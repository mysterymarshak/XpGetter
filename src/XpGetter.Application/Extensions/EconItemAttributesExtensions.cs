using SteamKit2.GC.CSGO.Internal;
using XpGetter.Application.Features.Cs2.Enums;

namespace XpGetter.Application.Extensions;

public static class EconItemAttributesExtensions
{
    extension(IEnumerable<CSOEconItemAttribute> attributes)
    {
        public void GetValueOrDefault(ItemAttribute attributeIndex, out uint value, uint defaultValue = 0)
        {
            var attribute = attributes.FirstOrDefault(x => x.def_index == (uint)attributeIndex);
            if (attribute is null)
            {
                value = defaultValue;
                return;
            }

            switch (attributeIndex)
            {
                case ItemAttribute.TexturePrefab:
                case ItemAttribute.TextureSeed:
                    value = (uint)BitConverter.ToSingle(attribute.value_bytes);
                    return;
                case ItemAttribute.StickerSlot0:
                case ItemAttribute.TintId:
                case ItemAttribute.KillEater:
                    value = BitConverter.ToUInt32(attribute.value_bytes);
                    return;
                default:
                    throw new IndexOutOfRangeException(nameof(attributeIndex));
            }
        }

        public void GetValueOrDefault(ItemAttribute attributeIndex, out float value, float defaultValue = 0f)
        {
            var attribute = attributes.FirstOrDefault(x => x.def_index == (uint)attributeIndex);
            if (attribute is null)
            {
                value = defaultValue;
                return;
            }

            switch (attributeIndex)
            {
                case ItemAttribute.TextureWear:
                    value = BitConverter.ToSingle(attribute.value_bytes);
                    return;
                default:
                    throw new IndexOutOfRangeException(nameof(attributeIndex));
            }
        }
    }
}
