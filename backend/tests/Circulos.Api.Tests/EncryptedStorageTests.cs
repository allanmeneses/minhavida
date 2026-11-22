using System.Security.Cryptography;
using System.Text;
using Circulos.Api.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Circulos.Api.Tests;

public sealed class EncryptedStorageTests
{
    private const string PlainText = "clausula confidencial que nao pode ficar em claro";

    [Fact]
    public async Task ArquivoNoDisco_NaoContemOTextoOriginal()
    {
        using var harness = new DiskHarness();
        var encrypted = harness.CreateStorage();

        const string storageKey = "dono/docs/segredo";
        await using (var input = new MemoryStream(Encoding.UTF8.GetBytes(PlainText)))
        {
            await encrypted.SaveAsync(storageKey, input, CancellationToken.None);
        }

        var path = harness.PathFor(storageKey);
        Assert.True(File.Exists(path));
        var raw = await File.ReadAllBytesAsync(path);
        var rawText = Encoding.UTF8.GetString(raw);

        Assert.DoesNotContain(PlainText, rawText, StringComparison.Ordinal);
        Assert.False(LooksLikeUtf8PlainText(raw), "O arquivo salvo no disco ainda parece texto em claro.");
        Assert.NotEqual(Encoding.UTF8.GetBytes(PlainText), raw);
        Assert.Equal(EncryptedFileStorage.Version, raw[0]);

        await using var opened = await encrypted.OpenReadAsync(storageKey, CancellationToken.None);
        using var reader = new StreamReader(opened, Encoding.UTF8);
        Assert.Equal(PlainText, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task FotoNoDisco_NaoMantemOCabecalhoPng()
    {
        using var harness = new DiskHarness();
        var encrypted = harness.CreateStorage();
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+X69gAAAAASUVORK5CYII=");

        const string storageKey = "dono/photo/foto.png";
        await using (var input = new MemoryStream(png))
        {
            await encrypted.SaveAsync(storageKey, input, CancellationToken.None);
        }

        var raw = await File.ReadAllBytesAsync(harness.PathFor(storageKey));
        Assert.False(raw.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.NotEqual(png, raw);

        await using var opened = await encrypted.OpenReadAsync(storageKey, CancellationToken.None);
        using var buffer = new MemoryStream();
        await opened.CopyToAsync(buffer);
        Assert.Equal(png, buffer.ToArray());
    }

    [Fact]
    public async Task ChaveErrada_NaoLeOArquivo()
    {
        using var harness = new DiskHarness();
        var encrypted = harness.CreateStorage();
        const string storageKey = "dono/docs/nota";
        await using (var input = new MemoryStream(Encoding.UTF8.GetBytes(PlainText)))
        {
            await encrypted.SaveAsync(storageKey, input, CancellationToken.None);
        }

        var otherKey = Convert.FromBase64String("AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE=");
        var other = new EncryptedFileStorage(harness.Local, new StaticEncryptionKeyProvider(otherKey));
        await Assert.ThrowsAnyAsync<CryptographicException>(() => other.OpenReadAsync(storageKey, CancellationToken.None));
    }

    [Fact]
    public void ChaveVemDaConfiguracao_NaoDeUmCampoDeBanco()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = TestEncryptionKey.Base64,
            })
            .Build();

        var provider = new ConfigurationEncryptionKeyProvider(configuration);
        Assert.Equal(TestEncryptionKey.Bytes, provider.GetKey());
    }

    private static bool LooksLikeUtf8PlainText(byte[] raw)
    {
        if (raw.Length == 0)
        {
            return false;
        }

        var text = Encoding.UTF8.GetString(raw);
        return text.All(ch => !char.IsControl(ch) || ch is '\n' or '\r' or '\t');
    }

    private sealed class DiskHarness : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "circulos-crypto-" + Guid.NewGuid().ToString("N"));
        public LocalFileStorage Local { get; }

        public DiskHarness()
        {
            Directory.CreateDirectory(Root);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:RootPath"] = Root,
                    ["Encryption:Key"] = TestEncryptionKey.Base64,
                })
                .Build();
            Local = new LocalFileStorage(configuration, new StubHost(Root));
        }

        public EncryptedFileStorage CreateStorage() =>
            new(Local, new StaticEncryptionKeyProvider(TestEncryptionKey.Bytes));

        public string PathFor(string key) => Path.Combine(Root, key);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class StubHost(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Circulos.Api.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
