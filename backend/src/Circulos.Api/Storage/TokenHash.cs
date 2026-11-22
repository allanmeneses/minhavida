using System.Security.Cryptography;
using System.Text;

namespace Circulos.Api.Storage;

public static class TokenHash
{
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
