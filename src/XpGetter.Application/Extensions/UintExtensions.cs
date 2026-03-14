namespace XpGetter.Application.Extensions;

public static class UintExtensions
{
    extension(uint number)
    {
        public ReadOnlySpan<char> FormatInto(Span<char> buffer)
        {
            return buffer[..(number.WriteToSpan(buffer))];
        }

        public ReadOnlySpan<byte> FormatInto(Span<byte> buffer)
        {
            return buffer[..(number.WriteToSpan(buffer))];
        }

        public int WriteToSpan(Span<byte> buffer)
        {
            if (!number.TryFormat(buffer, out var charsWritten))
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, null);
            }

            return charsWritten;
        }

        public int WriteToSpan(Span<char> buffer)
        {
            if (!number.TryFormat(buffer, out var charsWritten))
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, null);
            }

            return charsWritten;
        }
    }
}
