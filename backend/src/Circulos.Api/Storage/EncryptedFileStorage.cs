using System.Security.Cryptography;

namespace Circulos.Api.Storage;

public sealed class EncryptedFileStorage(IFileStorage inner, IEncryptionKeyProvider keys) : IFileStorage
{
    public const byte Version = 1;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int HeaderSize = 1 + NonceSize + TagSize;

    private readonly IFileStorage _inner = inner;
    private readonly byte[] _key = keys.GetKey();

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        using var plain = new MemoryStream();
        await content.CopyToAsync(plain, cancellationToken);
        var plaintext = plain.ToArray();
        var packed = Encrypt(plaintext);
        await using var cipher = new MemoryStream(packed, writable: false);
        await _inner.SaveAsync(key, cipher, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var stored = await _inner.OpenReadAsync(key, cancellationToken);
        using var buffer = new MemoryStream();
        await stored.CopyToAsync(buffer, cancellationToken);
        var plaintext = Decrypt(buffer.ToArray());
        return new MemoryStream(plaintext, writable: false);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(key, cancellationToken);

    private byte[] Encrypt(byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var packed = new byte[HeaderSize + ciphertext.Length];
        packed[0] = Version;
        Buffer.BlockCopy(nonce, 0, packed, 1, NonceSize);
        Buffer.BlockCopy(tag, 0, packed, 1 + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, packed, HeaderSize, ciphertext.Length);
        return packed;
    }

    private byte[] Decrypt(byte[] packed)
    {
        if (packed.Length < HeaderSize)
        {
            throw new CryptographicException("Arquivo cifrado incompleto.");
        }

        if (packed[0] != Version)
        {
            throw new CryptographicException("Versao de cifra nao suportada.");
        }

        var nonce = packed.AsSpan(1, NonceSize);
        var tag = packed.AsSpan(1 + NonceSize, TagSize);
        var ciphertext = packed.AsSpan(HeaderSize);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
