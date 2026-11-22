using Circulos.Api.Storage;

namespace Circulos.Api.Tests;

internal sealed class StaticEncryptionKeyProvider(byte[] key) : IEncryptionKeyProvider
{
    public byte[] GetKey() => key;
}

internal static class TestEncryptionKey
{
    public const string Base64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public static byte[] Bytes { get; } = Convert.FromBase64String(Base64);
}
