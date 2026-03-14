using System.Text;

namespace XpGetter.Application.Extensions;

public static class StringBuilderExtensions
{
    extension(StringBuilder sb)
    {
        public void AppendUtf8String(ReadOnlySpan<byte> bytes)
        {
            Span<char> buffer = stackalloc char[bytes.Length];
            sb.AppendUtf8String(bytes, buffer);
        }

        public void AppendUtf8String(ReadOnlySpan<byte> bytes, Span<char> buffer)
        {
            var charsWritten = Encoding.UTF8.GetChars(bytes, buffer);
            sb.Append(buffer[..charsWritten]);
        }
    }
}
