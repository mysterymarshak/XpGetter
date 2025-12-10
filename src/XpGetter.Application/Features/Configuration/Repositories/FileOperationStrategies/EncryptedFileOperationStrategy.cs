using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace XpGetter.Application.Features.Configuration.Repositories.FileOperationStrategies;

public class EncryptedFileOperationStrategy : IFileOperationStrategy
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public string ReadFileContent(string filePath)
    {
        Span<byte> key = stackalloc byte[KeySize];
        GetKey(key);

        var fileBytes = File.ReadAllBytes(filePath);
        if (fileBytes.Length < NonceSize + TagSize)
        {
            return string.Empty;
        }

        ReadOnlySpan<byte> fileSpan = fileBytes;
        var nonce = fileSpan.Slice(0, NonceSize);
        var tag = fileSpan.Slice(NonceSize, TagSize);
        var cipherBytes = fileSpan.Slice(NonceSize + TagSize);

        using var aes = new AesGcm(key, TagSize);
        var decryptedBytes = new byte[cipherBytes.Length];

        try
        {
            aes.Decrypt(nonce, cipherBytes, tag, decryptedBytes);
        }
        catch
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    public void WriteFileContent(string filePath, string content)
    {
        Span<byte> key = stackalloc byte[KeySize];
        GetKey(key);

        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        Span<byte> tag = stackalloc byte[TagSize];

        var bytes = Encoding.UTF8.GetBytes(content);
        var cipherBytes = new byte[bytes.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, bytes, cipherBytes, tag);

        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        stream.Write(nonce);
        stream.Write(tag);
        stream.Write(cipherBytes);
    }

    private void GetKey(Span<byte> keySpan)
    {
        const int maxSecretLength = 48;

        var uniqueSecret = GetUniqueSystemSecret().AsSpan();

        var uniqueSecretLength = Math.Min(uniqueSecret.Length, maxSecretLength);
        Span<byte> uniqueSecretBytes = stackalloc byte[uniqueSecretLength];
        Encoding.UTF8.GetBytes(uniqueSecret[..uniqueSecretLength], uniqueSecretBytes);

        using var sha256 = SHA256.Create();
        if (!sha256.TryComputeHash(uniqueSecretBytes, keySpan, out var bytesWritten) || bytesWritten != KeySize)
        {
            throw new InvalidOperationException();
        }
    }

    private string GetUniqueSystemSecret()
    {
        var osPlatform = Environment.OSVersion.Platform;
        var uniqueHardwareId = GetPlatformUniqueId();
        return $"{osPlatform}|{uniqueHardwareId}";
    }

    private string GetPlatformUniqueId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWmiHardwareIds();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxHardwareId();
        }

        throw new NotSupportedException();
    }

    private string GetLinuxHardwareId()
    {
        const string machineIdPath = "/etc/machine-id";
        if (File.Exists(machineIdPath))
        {
            return File.ReadAllText(machineIdPath).Trim();
        }

        return "Linux_ID_NotFound";
    }

    private string GetWmiHardwareIds()
    {
        var cpuId = GetWmiProperty("Win32_Processor", "ProcessorId");
        var motherboardSerial = GetWmiProperty("Win32_BaseBoard", "SerialNumber");
        return $"{cpuId}|{motherboardSerial}";
    }

    private string GetWmiProperty(string className, string propertyName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new InvalidOperationException();
        }

        var defaultValue = $"{propertyName}Unknown";

        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
            foreach (var obj in searcher.Get())
            {
                return obj[propertyName]?.ToString() ?? defaultValue;
            }
        }
        catch
        {
        }

        return defaultValue;
    }
}