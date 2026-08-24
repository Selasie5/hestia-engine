using HostelSystem.Application.Interfaces;
using QRCoder;

namespace HostelSystem.Infrastructure.Services.Payments;

public class QrCodeService : IQrCodeService
{
    public byte[] GeneratePng(string content, int pixelsPerModule = 20)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("QR content cannot be empty.", nameof(content));

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }

    public string GenerateSvg(string content, int pixelsPerModule = 20)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("QR content cannot be empty.", nameof(content));

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var svgQr = new SvgQRCode(data);
        return svgQr.GetGraphic(pixelsPerModule);
    }
}
