using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ProtoBuf;
using Serilog;
using SteamKit2.GC.CSGO.Internal;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Cs2.Enums;

namespace XpGetter.Application.Features.Cs2;

public class Cs2InventoryItem
{
    public required CSOEconItem EconItem { get; init; }
    public required string Name { get; init; }
    public required string HashName { get; init; }
    public required string RarityColor { get; init; }
    public required bool IsTradeable { get; init; }
    public PaintKit? PaintKit { get; init; }
}

// TODO: optimize memory
public record ItemDefinition(uint DefIndex, string Name, bool IsBaseItem, string RarityColor, bool IsTradeable);
public record Quality(uint Index, string Name, string Color);
public record PaintKit(uint Index, string DescriptionTag, string RarityColor, float? Wear);
public record StickerKit(string Name, string RarityColor);

public readonly ref struct Cs2InventoryParser
{
    private readonly CMsgClientWelcome _clientWelcome;
    private readonly ILogger _logger;
    private readonly KvTree.NodeAccessor _itemsGame;
    private readonly KvTree.NodeAccessor _localization;
    private readonly Dictionary<int, string> _itemsGameNodesValues;
    private readonly Dictionary<int, string> _localizationNodesValues;

    public Cs2InventoryParser(CMsgClientWelcome clientWelcome, KvTree itemsGame, KvTree localization, ILogger logger)
    {
        _clientWelcome = clientWelcome;
        _logger = logger;
        _itemsGame = itemsGame["items_game"];
        _localization = localization["lang"]["Tokens"];
        _itemsGameNodesValues = new Dictionary<int, string>();
        _localizationNodesValues = new Dictionary<int, string>();
    }

    public Dictionary<ulong, Cs2InventoryItem> Parse()
    {
        var inventory = new Dictionary<ulong, Cs2InventoryItem>();

        var cachedInventory = _clientWelcome
            .outofdate_subscribed_caches
            .FirstOrDefault()?
            .objects
            .FirstOrDefault(x => x.type_id == (uint)SoCacheType.Inventory);

        if (cachedInventory is null)
        {
            _logger.Error(Messages.Gc.InventoryNotFoundLog, _clientWelcome);
            return [];
        }

        var sb = new StringBuilder();
        foreach (var objectData in cachedInventory.object_data)
        {
            var econItem = Serializer.Deserialize<CSOEconItem>(objectData);
            var cs2InventoryItem = ParseEconItem(sb, econItem);
            inventory.Add(econItem.id, cs2InventoryItem);
            sb.Clear();
        }

        return inventory;
    }

    private Cs2InventoryItem ParseEconItem(StringBuilder sb, CSOEconItem econItem)
    {
        Span<char> digitsBuffer = stackalloc char[16];

        var cs2InventoryItemRaw = GetRawItem(econItem);

        var defIndex = cs2InventoryItemRaw.DefIndex;
        var defIndexSpan = defIndex.FormatInto(digitsBuffer);
        var itemDefinition = QueryItemDef(defIndex, defIndexSpan);

        var qualityIndex = cs2InventoryItemRaw.Quality;
        var qualityIndexSpan = qualityIndex.FormatInto(digitsBuffer);
        var quality = QueryQuality(qualityIndex, qualityIndexSpan);

        var paintIndex = cs2InventoryItemRaw.PaintIndex;
        var paintIndexSpan = paintIndex.FormatInto(digitsBuffer);
        var paintKit = QueryPaint(paintIndex, cs2InventoryItemRaw.Wear, paintIndexSpan);

        var hashName = GetHashName(in cs2InventoryItemRaw, itemDefinition, quality, paintKit, digitsBuffer, sb, out var name);

        return new Cs2InventoryItem
        {
            EconItem = econItem,
            Name = name,
            HashName = hashName,
            RarityColor = paintIndex > 0 ? paintKit!.RarityColor : quality.Color,
            PaintKit = paintKit,
            IsTradeable = itemDefinition.IsTradeable
        };
    }

    private ItemDefinition QueryItemDef(uint defIndex, ReadOnlySpan<char> defIndexSpan)
    {
        var defNode = _itemsGame["items"][defIndexSpan];

        var weaponHud = defNode["item_name"];
        var prefabName = defNode["prefab"];

        if (!TryGetStringValue(in weaponHud, out var weaponHudString, _itemsGameNodesValues))
        {
            var prefabItemName = _itemsGame["prefabs"][prefabName.Value]["item_name"];
            weaponHudString = GetStringValue(in prefabItemName, _itemsGameNodesValues);
        }

        var isBaseItem = defNode["baseitem"].TryGetValue(out _);

        var rarity = defNode["item_rarity"];
        var rarityColorString = QueryRarityColor(in rarity);

        var isTradeable = !IsNotTradeable(in defNode);

        return new ItemDefinition(
            defIndex,
            weaponHudString,
            isBaseItem,
            rarityColorString,
            isTradeable);
    }

    private bool IsNotTradeable(in KvTree.NodeAccessor itemDefNode)
    {
        var node = itemDefNode;
        while (node.Index != -1)
        {
            if (node["attributes"]["cannot trade"].TryGetValue(out _))
            {
                return true;
            }

            var prefabNameLeaf = node["prefab"];
            if (!prefabNameLeaf.TryGetValue(out var prefabNameSpan))
            {
                return false;
            }

            node = _itemsGame["prefabs"][prefabNameSpan];
        }

        return false;
    }

    private Quality QueryQuality(uint qualityIndex, ReadOnlySpan<char> qualityIndexSpan)
    {
        var quality = _itemsGame["qualities"].FindByChildValue("value", qualityIndexSpan);
        var name = quality.StringKey;
        var color = quality["hexColor"];
        return new Quality(qualityIndex, name, GetStringValue(in color, _itemsGameNodesValues));
    }

    private Cs2InventoryItemRaw GetRawItem(CSOEconItem item)
    {
        var attributes = item.attribute;

        attributes.GetValueOrDefault(ItemAttribute.TexturePrefab, out uint paint);
        attributes.GetValueOrDefault(ItemAttribute.TextureWear, out float floatWear, -1f);
        attributes.GetValueOrDefault(ItemAttribute.KillEater, out uint stattrakCount, defaultValue: uint.MaxValue);
        attributes.GetValueOrDefault(ItemAttribute.TintId, out uint tintId);

        // get sticker attribute only if item is not a weapon (doesnt have paint)
        var sprayId = 0u;
        if (paint == 0)
        {
            attributes.GetValueOrDefault(ItemAttribute.StickerSlot0, out sprayId);
        }

        float? wear = floatWear == -1f ? null : floatWear;
        var isStattrak = stattrakCount != uint.MaxValue;

        return new Cs2InventoryItemRaw(item.def_index, item.quality, paint, sprayId, tintId, wear, isStattrak);
    }

    private string GetHashName(
        in Cs2InventoryItemRaw cs2InventoryItemRaw,
        ItemDefinition itemDefinition,
        Quality quality,
        PaintKit? paintKit,
        Span<char> digitsBuffer,
        StringBuilder sb,
        out string name)
    {
        if (IsQualityPrefixSpecial(quality.Index))
        {
            sb.Append(QueryName(quality.Name)); // e.g. Souvenir

            if (quality.Index is (uint)ItemQuality.Unusual && cs2InventoryItemRaw.IsStattrak) // ★ StatTrak
            {
                sb.Append(' ');
                sb.Append(QueryName("strange"));
            }

            sb.Append(' ');
        }

        var defName = QueryName(itemDefinition.Name.TrimFirstChar('#')); // e.g. AWP
        sb.Append(defName);

        if (cs2InventoryItemRaw.PaintIndex > 0)
        {
            sb.Append(" | ");
            sb.Append(QueryName(paintKit!.DescriptionTag.TrimFirstChar('#'))); // e.g. Dragon Lore
        }

        if (cs2InventoryItemRaw.SprayId > 0)
        {
            sb.Append(" | ");
            sb.Append(QuerySprayName(cs2InventoryItemRaw.SprayId, cs2InventoryItemRaw.TintId, digitsBuffer)); // e.g. Popdog (Bazooka Pink)
        }

        // in case of equality HashName and Name we don't allocate extra string so they're sharing same instance
        string? tempName = null;

        string? wearName = null; // Field-Tested
        if (cs2InventoryItemRaw.Wear is not null)
        {
            wearName = QueryWearName(cs2InventoryItemRaw.Wear.Value);
        }
        if (!string.IsNullOrWhiteSpace(wearName))
        {
            tempName = sb.ToString();
            sb.Append(' ');
            sb.Append('(');
            sb.Append(wearName);
            sb.Append(')');
        }

        var result = sb.ToString();
        tempName ??= result;
        name = tempName;
        return result;
    }

    private bool IsQualityPrefixSpecial(uint quality)
    {
        return quality is (uint)ItemQuality.Unusual or (uint)ItemQuality.Strange or (uint)ItemQuality.Tournament;
    }

    private string QueryWearName(double wear)
    {
        var wearName = wear switch
        {
            <= 0.07 =>            "SFUI_InvTooltip_Wear_Amount_0",
            > 0.07 and <= 0.15 => "SFUI_InvTooltip_Wear_Amount_1",
            > 0.15 and <= 0.38 => "SFUI_InvTooltip_Wear_Amount_2",
            > 0.38 and <= 0.45 => "SFUI_InvTooltip_Wear_Amount_3",
            > 0.45 =>             "SFUI_InvTooltip_Wear_Amount_4",
            _ => throw new ArgumentOutOfRangeException(nameof(wear), wear, null)
        };

        return QueryName(wearName);
    }

    private PaintKit? QueryPaint(uint paintIndex, float? wear, ReadOnlySpan<char> paintIndexSpan)
    {
        if (paintIndex == 0)
        {
            return null;
        }

        var paintKit = _itemsGame["paint_kits"][paintIndexSpan];
        var name = paintKit["name"];
        var nameString = GetStringValue(in name, _itemsGameNodesValues);
        var descriptionTag = paintKit["description_tag"];
        var paintKitRarity = _itemsGame["paint_kits_rarity"][nameString];
        var rarityColorString = QueryRarityColor(in paintKitRarity);

        return new PaintKit(
            paintIndex,
            GetStringValue(in descriptionTag, _itemsGameNodesValues),
            rarityColorString,
            wear);
    }

    // stickers and graffities literally have the same parent "sticker_kits"
    // so because of that reason this method is so confusing like tf i query sticker kit if i need a graffiti
    // valve moment absolute cinema
    private string QuerySprayName(uint sprayId, uint tintId, Span<char> digitsBuffer)
    {
        var sprayIdSpan = sprayId.FormatInto(digitsBuffer);
        var spray = QueryStickerKit(sprayIdSpan);

        if (tintId == 0)
        {
            return QuerySprayName(spray, ReadOnlySpan<char>.Empty);
        }

        var tintIdSpan = tintId.FormatInto(digitsBuffer);
        return QuerySprayName(spray, tintIdSpan);
    }

    private StickerKit QueryStickerKit(ReadOnlySpan<char> stickerId)
    {
        var stickerKit = _itemsGame["sticker_kits"][stickerId];
        var name = stickerKit["item_name"];
        var rarity = stickerKit["item_rarity"];

        return new StickerKit(
            GetStringValue(in name, _itemsGameNodesValues),
            QueryRarityColor(in rarity));
    }

    private string QuerySprayName(StickerKit spray, ReadOnlySpan<char> tintId)
    {
        Span<char> sprayNameSpan = stackalloc char[64];
        var cursor = 0;

        // Popdog
        var sprayName = QueryNameRaw(spray.Name.TrimFirstChar('#'));
        Ascii.ToUtf16(sprayName, sprayNameSpan, out var charsWritten);
        cursor += charsWritten;

        if (!tintId.IsEmpty)
        {
            const string tintPrefix = "Attrib_SprayTintValue_";

            tintPrefix.CopyTo(sprayNameSpan[cursor..]);
            tintId.CopyTo(sprayNameSpan[(cursor + tintPrefix.Length)..]);

            // (Bazooka Pink)
            var tintName = QueryNameRaw(sprayNameSpan[cursor..(cursor + tintPrefix.Length + tintId.Length)]);

            sprayNameSpan[cursor++] = ' ';
            sprayNameSpan[cursor++] = '(';
            Ascii.ToUtf16(tintName, sprayNameSpan[cursor..], out charsWritten);
            cursor += charsWritten;
            sprayNameSpan[cursor++] = ')';
        }

        return sprayNameSpan[..cursor].ToString();
    }

    private string QueryRarityColor(in KvTree.NodeAccessor rarityLeaf)
    {
        KvTree.NodeAccessor rarity;

        if (rarityLeaf.TryGetValue(out var rarityLeafValue))
        {
            rarity = _itemsGame["rarities"][rarityLeafValue];
        }
        else
        {
            rarity = _itemsGame["rarities"]["default"];
        }

        var color = _itemsGame["colors"][rarity["color"].Value]["hex_color"];
        return GetStringValue(in color, _itemsGameNodesValues);
    }

    private ReadOnlySpan<byte> QueryNameRaw(ReadOnlySpan<char> key)
    {
        return _localization[key].Value;
    }

    private string QueryName(ReadOnlySpan<char> key)
    {
        var leaf = _localization[key];
        return GetStringValue(in leaf, _localizationNodesValues);
    }

    private bool TryGetStringValue(
        in KvTree.NodeAccessor node,
        [NotNullWhen(true)] out string? result,
        Dictionary<int, string> cache)
    {
        ref var existingValue = ref CollectionsMarshal.GetValueRefOrNullRef(cache, node.Index);
        if (!Unsafe.IsNullRef(ref existingValue))
        {
            result = existingValue;
            return true;
        }

        if (!node.TryGetValue(out var nodeValue))
        {
            result = null;
            return false;
        }

        result = Encoding.UTF8.GetString(nodeValue);
        cache.Add(node.Index, result);
        return true;
    }

    private string GetStringValue(in KvTree.NodeAccessor node, Dictionary<int, string> cache)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(cache, node.Index, out var exists);
        if (!exists)
        {
            value = node.StringValue;
        }

        return value!;
    }

    private readonly ref struct Cs2InventoryItemRaw
    {
        public readonly uint DefIndex;
        public readonly uint Quality;
        public readonly uint PaintIndex;
        public readonly uint SprayId;
        public readonly uint TintId;
        public readonly float? Wear;
        public readonly bool IsStattrak;

        public Cs2InventoryItemRaw(uint defIndex, uint quality, uint paintIndex, uint sprayId,
            uint tintId, float? wear, bool isStattrak)
        {
            DefIndex = defIndex;
            Quality = quality;
            PaintIndex = paintIndex;
            SprayId = sprayId;
            TintId = tintId;
            Wear = wear;
            IsStattrak = isStattrak;
        }
    }
}
