using Net.Codecrete.QrCodeGenerator;
using SkiaSharp;

namespace Circulos.Api.Auth;

public static class TotpQr
{
    public static byte[] Png(string otpauthUri)
    {
        var qr = QrCode.EncodeText(otpauthUri, QrCode.Ecc.Medium);
        const int scale = 8;
        const int border = 2;
        var dim = (qr.Size + (border * 2)) * scale;
        using var bitmap = new SKBitmap(dim, dim);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false };
        for (var y = 0; y < qr.Size; y++)
        {
            for (var x = 0; x < qr.Size; x++)
            {
                if (qr.GetModule(x, y))
                {
                    canvas.DrawRect((x + border) * scale, (y + border) * scale, scale, scale, paint);
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
