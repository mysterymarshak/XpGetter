using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Io;

namespace XpGetter.Application.Features.Cs2;

public enum KvFileType
{
    None = 0,
    ItemsGame,
    Localization
}

public struct Kv
{
    public readonly int KeyOffset;
    public readonly int ValueOffset;
    public int FirstChild;
    public int NextSibling;
    public readonly short KeyLength;
    public readonly short ValueLength;

    public Kv(int keyOffset, int keyLength, int valueOffset, int valueLength)
    {
        KeyOffset = keyOffset;
        KeyLength = (short)keyLength;
        ValueOffset = valueOffset;
        ValueLength = (short)valueLength;
        FirstChild = -1;
        NextSibling = -1;
    }
}

public readonly ref struct KvParser
{
    private const int MaxDepth = 16;
    private const bool FilterItemsGameNodesByName = true;
    private static readonly byte[][] NamesToSearch =
    [
        "items"u8.ToArray(),
        "qualities"u8.ToArray(),
        "paint_kits"u8.ToArray(),
        "sticker_kits"u8.ToArray(),
        "prefabs"u8.ToArray(),
        "colors"u8.ToArray(),
        "rarities"u8.ToArray(),
        "paint_kits_rarity"u8.ToArray()
    ];

    private readonly KvFileType _type;
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly List<Kv> _nodes;

    public KvParser(KvFileType type, ReadOnlySpan<byte> buffer)
    {
        _type = type;
        _buffer = buffer;
        _nodes = CreateTree();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public List<Kv> Parse()
    {
        var reader = new KvReader(_buffer);
        var depth = 0;

        Span<int> parents = stackalloc int[MaxDepth];
        parents.Fill(-1);

        Span<int> lastSibling = stackalloc int[MaxDepth];
        lastSibling.Fill(-1);

        var isAwaitingNodeOrLeaf = false;
        var nameToken = Token.Empty;

        while (true)
        {
            var token = reader.GetToken();

            if (token.Kind == TokenKind.DQuote)
            {
                if (isAwaitingNodeOrLeaf)
                {
                    isAwaitingNodeOrLeaf = false;
                    ReadValue(out var valueToken, ref reader);

                    AddLeaf(in nameToken, in valueToken, in parents, in lastSibling, depth);

                    continue;
                }

                ReadName(out nameToken, ref reader);
                isAwaitingNodeOrLeaf = true;

                continue;
            }

            if (token.Kind == TokenKind.OpenBracket)
            {
                isAwaitingNodeOrLeaf = false;

                var node = new Kv(nameToken.Position, nameToken.Length, -1, -1);

                var shouldEat = ShouldEat(in node, depth);
                if (shouldEat)
                {
                    reader.EatNode();
                    continue;
                }

                var nodeIndex = AddKv(in node, in parents, in lastSibling, depth);

                depth++;
                parents[depth] = nodeIndex;

                continue;
            }

            if (token.Kind == TokenKind.CloseBracket)
            {
                lastSibling[depth] = -1;
                parents[depth] = -1;
                depth--;

                continue;
            }

            if (token.Kind == TokenKind.EndOfStream)
            {
                break;
            }
        }

        return _nodes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ReadName(out Token nameToken, scoped ref KvReader reader)
    {
        nameToken = reader.GetToken();
        ExpectTokenKind(in nameToken, TokenKind.String);

        var dqToken = reader.GetToken();
        ExpectTokenKind(in dqToken, TokenKind.DQuote);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ReadValue(out Token valueToken, scoped ref KvReader reader)
    {
        valueToken = reader.ReadAsString();
        ExpectTokenKind(in valueToken, TokenKind.String);

        var dqToken = reader.GetToken();
        ExpectTokenKind(in dqToken, TokenKind.DQuote);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ExpectTokenKind(scoped in Token token, TokenKind kind)
    {
        if (token.Kind != kind)
        {
            // TODO: better exception
            throw new InvalidOperationException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void AddLeaf(scoped in Token nameToken, scoped in Token valueToken,
        in Span<int> parents, in Span<int> lastSibling, int depth)
    {
        var leaf = new Kv(nameToken.Position, nameToken.Length,
            valueToken.Position, valueToken.Length);

        AddKv(in leaf, in parents, in lastSibling, depth);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int AddKv(scoped in Kv kv, in Span<int> parents, in Span<int> lastSibling, int depth)
    {
        _nodes.Add(kv);
        var kvIndex = _nodes.Count - 1;

        var span = CollectionsMarshal.AsSpan(_nodes);

        var lastSiblingIndex = lastSibling[depth];
        if (lastSiblingIndex != -1)
        {
            ref var sibling = ref span[lastSiblingIndex];
            sibling.NextSibling = kvIndex;
        }

        lastSibling[depth] = kvIndex;

        var parentIndex = parents[depth];
        if (parentIndex != -1)
        {
            ref var parent = ref span[parentIndex];
            if (parent.FirstChild == -1)
            {
                parent.FirstChild = kvIndex;
            }
        }

        return kvIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool ShouldEat(in Kv node, int depth)
    {
        if (depth == 1 && _type == KvFileType.ItemsGame && FilterItemsGameNodesByName)
        {
            var name = GetKey(in node);
            foreach (var nameToSearch in NamesToSearch)
            {
                if (name.SequenceEqual(nameToSearch))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ReadOnlySpan<byte> GetKey(scoped in Kv node)
    {
        return _buffer.Slice(node.KeyOffset, node.KeyLength);
    }

    private List<Kv> CreateTree()
    {
        var capacity = _type switch
        {
            // FilterItemsGameNodesByName: false
            // for now items_game take about 192k kv instances so 200k will be good enough
            // we can't know the exact amount of them before parsing so we needa allocate some extra space
            // i use lists in case of future overflowing so it will just grow if kvs count will be greater than capacity
            // CountLines will return ~250k for now
            // this approach works with localization since almost every single entry is a leaf
            // so for items_game i use predefined constant instead of counting lines since it too bigger than actual size i need
            // ////////////////
            // FilterItemsGameNodesByName: true
            // for XpGetter im interested only for specific nodes (NamesToSearch)
            // so for now parser creates about 128k kv instances
            // that means i could use 135k capacity to have some space in case of adding new items, etc..
            KvFileType.ItemsGame => FilterItemsGameNodesByName ? 135_000 : 200_000,
            KvFileType.Localization => _buffer.CountLines(),
            _ => throw new ArgumentOutOfRangeException(nameof(_type))
        };

        return new List<Kv>(capacity);
    }

    private enum TokenKind
    {
        None = 0,
        DQuote,
        String,
        EndOfStream,
        OpenBracket,
        CloseBracket
    }

    private readonly ref struct Token
    {
        public static Token Empty => new Token();

        public TokenKind Kind { get; }
        public int Position { get; }
        public int Length { get; }

        public Token(TokenKind kind, int position, int length)
        {
            Position = position;
            Length = length;
            Kind = kind;
        }
    }

    private ref struct KvReader
    {
        // some black magic to speed up the memory access (by removing boundary checks)
        // since getter of this property is called on the order of 10M times we may improve performance
        // on my machine by doing that i could have about -10% of parsing time
        // but obviously this check is unsafe, like what if i get an invalid items_game.txt file or something?
        // (because of validation purposes i also have ExpectTokenKind method in tree class which also take some execution time)
        // if we have boundary checks, then parser will just exit with an exception, but if not
        // we will encounter unexpected behavior and bad memory accessing
        // so for Current property i use "boundary" access
        // but if i really wanna use ⚡ insanely ⚡ fast method i have an indexer
        // (it used with the values less than _cursor so probably its safe)

        // private readonly ref readonly byte Current => ref Unsafe.Add(ref _bufferReference, _cursor);
        private char Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => (char)_buffer[_cursor];
        }

        private readonly ref readonly byte this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => ref Unsafe.Add(ref _bufferReference, index);
        }

        private readonly ReadOnlySpan<byte> _buffer;
        private readonly ref byte _bufferReference;

        private int _cursor;

        public KvReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer.TrimEnd();
            _bufferReference = ref MemoryMarshal.GetReference(_buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public Token GetToken()
        {
            if (_cursor >= _buffer.Length - 1)
            {
                return new Token(TokenKind.EndOfStream, _cursor, 0);
            }

            while (EatWhiteSpace()) ;

            var start = _cursor;

            if (Current is '"')
            {
                _cursor++;
                return new Token(TokenKind.DQuote, start, 1);
            }

            if (Current is '{')
            {
                _cursor++;
                return new Token(TokenKind.OpenBracket, start, 1);
            }

            if (Current is '}')
            {
                _cursor++;
                return new Token(TokenKind.CloseBracket, start, 1);
            }

            if (Current is '/')
            {
                SkipLine();
                return GetToken();
            }

            return ReadAsString(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public Token ReadAsString(int start = -1)
        {
            if (start == -1)
            {
                start = _cursor;
            }

            var length = ReadString();
            return new Token(TokenKind.String, start, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int EatNode()
        {
            var charRead = 0;
            var openedBraces = 1;

            while (true)
            {
                if (Current is '{')
                {
                    openedBraces++;
                }
                else if (Current is '}')
                {
                    openedBraces--;
                }

                if (openedBraces == 0)
                {
                    _cursor++;
                    break;
                }

                charRead++;
                _cursor++;
            }

            return charRead;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private bool EatWhiteSpace()
        {
            if (char.IsWhiteSpace(Current))
            {
                _cursor++;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void SkipLine()
        {
            while (!IsNewline())
            {
                _cursor++;
            }

            _cursor++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private int ReadString()
        {
            var charRead = 0;

            while (true)
            {
                if (Current is '"')
                {
                    if (this[_cursor - 1] != '\\')
                    {
                        break;
                    }
                }

                charRead++;
                _cursor++;
            }

            return charRead;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private bool IsNewline() => Current is '\n' or '\r';
    }
}

public class KvTree : IDisposable
{
    public NodeAccessor this[ReadOnlySpan<char> key] => new(this, GetNode(-1, key));

    private readonly unsafe byte* _bufferPtr;
    private readonly int _bufferLength;
    private readonly MemoryMappedFile? _mmf;
    private readonly MemoryMappedViewAccessor? _accessor;

    private List<Kv>? _nodes;
    private MemoryHandle _pin;

    private unsafe KvTree(int bufferLength, MemoryMappedFile mmf)
    {
        _bufferLength = bufferLength;
        _mmf = mmf;
        _accessor = _mmf.CreateViewAccessor(0, _bufferLength, MemoryMappedFileAccess.Read);
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _bufferPtr);
    }

    private unsafe KvTree(ReadOnlyMemory<byte> bufferArray)
    {
        _pin = bufferArray.Pin();
        _bufferPtr = (byte*)_pin.Pointer;
        _bufferLength = bufferArray.Length;
    }

    public static KvTree ReadFromFile(KvFileType type, string filePath, IFilesAccessor filesAccessor)
    {
        var bufferLength = (int)filesAccessor.GetInfo(filePath).Length;
        var mmf = filesAccessor.CreateReadonlyMemoryMapping(filePath);

        var tree = new KvTree(bufferLength, mmf);
        tree.Parse(type);
        return tree;
    }

    public static KvTree ReadFromBytes(KvFileType type, ReadOnlyMemory<byte> bytes, IFilesAccessor filesAccessor)
    {
        var tree = new KvTree(bytes);

        tree.Parse(type);
        return tree;
    }

    public override string ToString()
    {
        if (_nodes?.Count > 0)
        {
            var sb = new StringBuilder();
            AppendNodesAtDepth(sb, 0, 0);
            return sb.ToString();
        }

        return "{}";
    }

    public unsafe void Dispose()
    {
        _accessor?.SafeMemoryMappedViewHandle.ReleasePointer();
        _mmf?.Dispose();
        _accessor?.Dispose();
        if (_pin.Pointer != null)
        {
            _pin.Dispose();
        }
    }

    private unsafe void Parse(KvFileType type)
    {
        var parser = new KvParser(type, new ReadOnlySpan<byte>(_bufferPtr, _bufferLength));
        _nodes = parser.Parse();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int GetNode(int parentIndex, ReadOnlySpan<char> key)
    {
        if (parentIndex == -1)
        {
            return FindSiblingWithName(0, key);
        }

        var parent = _nodes![parentIndex];
        return GetNode(in parent, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private int GetNode(in Kv parent, ReadOnlySpan<char> key)
    {
        return FindSiblingWithName(parent.FirstChild, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int FindSiblingWithName(int nodeIndex, ReadOnlySpan<char> name)
    {
        while (nodeIndex != -1)
        {
            var node = _nodes![nodeIndex];
            if (IsNamed(in node, name))
            {
                break;
            }

            nodeIndex = node.NextSibling;
        }

        return nodeIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private int FindSiblingWithName(int nodeIndex, ReadOnlySpan<byte> name)
    {
        while (nodeIndex != -1)
        {
            var node = _nodes![nodeIndex];
            if (IsNamed(in node, name))
            {
                break;
            }

            nodeIndex = node.NextSibling;
        }

        return nodeIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private string GetNodeString(int nodeIndex)
    {
        EnsureBoundaries(nodeIndex);

        var node = _nodes![nodeIndex];
        if (node.FirstChild == -1)
        {
            return GetLeafValue(in node);
        }

        var sb = new StringBuilder();
        AppendSiblingsByName(sb, nodeIndex, GetKey(in node));
        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private string GetLeafValue(in Kv leaf)
    {
        var value = GetValue(in leaf);
        return Encoding.UTF8.GetString(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void EnsureBoundaries(int nodeIndex)
    {
        if (!IsInBounds(nodeIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeIndex));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsInBounds(int nodeIndex) => nodeIndex >= 0 && nodeIndex < _nodes!.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsNamed(in Kv node, ReadOnlySpan<char> name)
    {
        return Ascii.EqualsIgnoreCase(GetKey(in node), name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool IsNamed(in Kv node, ReadOnlySpan<byte> name)
    {
        return Ascii.EqualsIgnoreCase(GetKey(in node), name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ReadOnlySpan<byte> GetKeySafe(int nodeIdx)
    {
        if (nodeIdx < 0 || nodeIdx >= _nodes!.Count)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        var node = _nodes![nodeIdx];
        return GetKey(in node);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ReadOnlySpan<byte> GetKey(int nodeIdx)
    {
        var node = _nodes![nodeIdx];
        return GetKey(in node);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe ReadOnlySpan<byte> GetKey(scoped in Kv node)
    {
        return new ReadOnlySpan<byte>(Unsafe.Add<byte>(_bufferPtr, node.KeyOffset), node.KeyLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool TryGetValue(int leafIndex, out ReadOnlySpan<byte> value)
    {
        if (!IsInBounds(leafIndex))
        {
            value = ReadOnlySpan<byte>.Empty;
            return false;
        }

        var leaf = _nodes![leafIndex];
        value = GetValue(in leaf);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ReadOnlySpan<byte> GetValue(int leafIndex)
    {
        var leaf = _nodes![leafIndex];
        return GetValue(in leaf);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private unsafe ReadOnlySpan<byte> GetValue(scoped in Kv leaf)
    {
        return new ReadOnlySpan<byte>(Unsafe.Add<byte>(_bufferPtr, leaf.ValueOffset), leaf.ValueLength);
    }

    private void AppendNodesAtDepth(StringBuilder sb, int nodeIndex, int depth)
    {
        var siblingIndex = nodeIndex;
        const int maxStringLength = 4096;
        // TODO: accept buffer as parameter
        Span<char> buffer = stackalloc char[maxStringLength];

        while (siblingIndex != -1)
        {
            var node = _nodes![siblingIndex];
            sb.Append(' ', depth * 4);
            sb.Append('"');
            sb.AppendUtf8String(GetKey(siblingIndex), buffer);
            sb.Append('"');

            if (node.ValueOffset != -1)
            {
                sb.Append(' ');
                sb.Append('"');
                sb.AppendUtf8String(GetValue(siblingIndex), buffer);
                sb.Append('"');
                sb.Append(Environment.NewLine);
            }
            else
            {
                sb.Append(Environment.NewLine);
                sb.Append(' ', depth * 4);
                sb.Append('{');
                sb.Append(Environment.NewLine);

                var childIndex = node.FirstChild;
                if (childIndex != -1)
                {
                    AppendNodesAtDepth(sb, childIndex, depth + 1);
                }

                sb.Append(' ', depth * 4);
                sb.Append('}');
                sb.Append(Environment.NewLine);
            }

            siblingIndex = node.NextSibling;
        }
    }

    private void AppendSiblingsByName(StringBuilder sb, int nodeIndex, ReadOnlySpan<byte> name)
    {
        sb.Append('"');
        sb.AppendUtf8String(name);
        sb.Append('"');
        sb.Append(Environment.NewLine);
        sb.Append('{');
        sb.Append(Environment.NewLine);

        var siblingIndex = nodeIndex;
        while (siblingIndex != -1)
        {
            var node = _nodes![siblingIndex];
            if (IsNamed(in node, name))
            {
                var childIndex = node.FirstChild;
                if (childIndex == -1)
                {
                    continue;
                }

                AppendNodesAtDepth(sb, childIndex, 1);
            }

            siblingIndex = node.NextSibling;
        }

        sb.Append('}');
    }

    public readonly ref struct NodeAccessor
    {
        public NodeAccessor this[scoped ReadOnlySpan<char> key]
        {
            get
            {
                if (Index == -1)
                {
                    return NotFound();
                }

                _tree.EnsureBoundaries(Index);

                var currentIndex = Index;
                while (currentIndex != -1)
                {
                    var currentNode = _tree._nodes![currentIndex];
                    var childIndex = _tree.GetNode(in currentNode, key);
                    if (childIndex != -1)
                    {
                        return new NodeAccessor(_tree, childIndex);
                    }

                    currentIndex = _tree.FindSiblingWithName(currentNode.NextSibling, _name);
                }

                return NotFound();
            }
        }

        public NodeAccessor this[ReadOnlySpan<byte> key]
        {
            get
            {
                Span<char> buffer = stackalloc char[key.Length];
                Ascii.ToUtf16(key, buffer, out var charsWritten);
                return this[buffer[..charsWritten]];
            }
        }

        public string StringKey => Encoding.UTF8.GetString(_name);
        public string StringValue => _tree.GetNodeString(Index);
        public ReadOnlySpan<byte> Value => _tree.GetValue(Index);

        public readonly int Index;

        private readonly KvTree _tree;
        private readonly ReadOnlySpan<byte> _name;

        public NodeAccessor(KvTree tree, int index)
        {
            _tree = tree;
            Index = index;
            _name = _tree.GetKeySafe(index);
        }

        public NodeAccessor FindByChildValue(ReadOnlySpan<char> childKey, ReadOnlySpan<char> targetValue)
        {
            if (Index == -1)
            {
                return NotFound();
            }

            _tree.EnsureBoundaries(Index);

            var currentIndex = _tree._nodes![Index].FirstChild;
            while (currentIndex != -1)
            {
                var currentNode = new NodeAccessor(_tree, currentIndex);
                if (Ascii.Equals(currentNode[childKey].Value, targetValue))
                {
                    return currentNode;
                }

                var currentKv = _tree._nodes![currentIndex];
                currentIndex = currentKv.NextSibling;
            }

            return NotFound();
        }

        public bool TryGetValue(out ReadOnlySpan<byte> value)
        {
            return _tree.TryGetValue(Index, out value);
        }

        private NodeAccessor NotFound()
        {
            return new NodeAccessor(_tree, -1);
        }
    }
}
