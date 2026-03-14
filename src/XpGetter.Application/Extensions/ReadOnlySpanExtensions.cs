namespace XpGetter.Application.Extensions;

public static class ReadOnlySpanExtensions
{
    extension(ReadOnlySpan<byte> buffer)
    {
        public ReadOnlySpan<byte> TrimEnd()
        {
            var index = buffer.Length - 1;
            while (index >= 0 && char.IsWhiteSpace((char)buffer[index]))
            {
                index--;
            }

            return buffer[..(index + 1)];
        }

        public int CountLines()
        {
            return buffer.Count((byte)'\n');
        }
    }
}
