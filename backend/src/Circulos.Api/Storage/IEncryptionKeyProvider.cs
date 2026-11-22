namespace Circulos.Api.Storage;

public interface IEncryptionKeyProvider
{
    byte[] GetKey();
}
