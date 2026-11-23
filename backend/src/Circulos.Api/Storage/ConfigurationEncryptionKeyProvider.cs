namespace Circulos.Api.Storage;

public sealed class ConfigurationEncryptionKeyProvider(IConfiguration configuration) : IEncryptionKeyProvider
{
    public byte[] GetKey()
    {
        var value = configuration["Encryption:Key"];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Encryption:Key não configurada.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Encryption:Key precisa ser Base64 de 32 bytes.", exception);
        }

        if (key.Length != 32)
        {
            throw new InvalidOperationException("Encryption:Key precisa ter 32 bytes (AES-256).");
        }

        return key;
    }
}
