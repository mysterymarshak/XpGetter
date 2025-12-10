using QRCoder;

namespace XpGetter.Application.Utils;

public static class QrCode
{
    // TODO: move to Cli
    // TODO: doesnt work properly on windows
    public static void DrawSmallestAscii(string content)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.L);
        using var qrCode = new AsciiQRCode(qrData);
        var qrCodeAsAsciiArt = qrCode.GetGraphicSmall(drawQuietZones: false, invert: true);
        Console.WriteLine(qrCodeAsAsciiArt);
    }
}
