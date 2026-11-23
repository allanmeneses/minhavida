using SkiaSharp;

namespace Circulos.Api.Storage;

public static class ImageCompressor
{
    public const int MaxEdge = 1080;
    public const int JpegQuality = 82;

    public static async Task<CompressedImage> CompressAsync(Stream input, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        using var bitmap = SKBitmap.Decode(buffer.ToArray());
        if (bitmap is null)
        {
            throw new InvalidDataException("A foto não pode ser lida.");
        }

        SKBitmap working = bitmap;
        SKBitmap? resized = null;
        try
        {
            var longest = Math.Max(bitmap.Width, bitmap.Height);
            if (longest > MaxEdge)
            {
                var ratio = MaxEdge / (double)longest;
                var width = Math.Max(1, (int)Math.Round(bitmap.Width * ratio));
                var height = Math.Max(1, (int)Math.Round(bitmap.Height * ratio));
                resized = bitmap.Resize(
                    new SKImageInfo(width, height),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                if (resized is null)
                {
                    throw new InvalidDataException("Não foi possível redimensionar a foto.");
                }

                working = resized;
            }

            using var image = SKImage.FromBitmap(working);
            using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
                ?? throw new InvalidDataException("Não foi possível comprimir a foto.");
            var output = new MemoryStream();
            encoded.SaveTo(output);
            output.Position = 0;
            return new CompressedImage(output, "image/jpeg", "foto.jpg", output.Length);
        }
        finally
        {
            resized?.Dispose();
        }
    }
}

public sealed record CompressedImage(Stream Content, string ContentType, string FileName, long SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        Content.Dispose();
        return ValueTask.CompletedTask;
    }
}
