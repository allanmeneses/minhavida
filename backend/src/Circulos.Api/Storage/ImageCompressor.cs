using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Circulos.Api.Storage;

public static class ImageCompressor
{
    public const int MaxEdge = 1080;
    public const int JpegQuality = 82;

    public static async Task<CompressedImage> CompressAsync(Stream input, CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(input, cancellationToken);
        var longest = Math.Max(image.Width, image.Height);
        if (longest > MaxEdge)
        {
            var ratio = MaxEdge / (double)longest;
            var width = Math.Max(1, (int)Math.Round(image.Width * ratio));
            var height = Math.Max(1, (int)Math.Round(image.Height * ratio));
            image.Mutate(x => x.Resize(width, height));
        }

        var output = new MemoryStream();
        var encoder = new JpegEncoder { Quality = JpegQuality };
        await image.SaveAsJpegAsync(output, encoder, cancellationToken);
        output.Position = 0;
        return new CompressedImage(output, "image/jpeg", "foto.jpg", output.Length);
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
