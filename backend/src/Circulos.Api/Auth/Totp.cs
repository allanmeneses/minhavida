using System.Security.Cryptography;
using System.Text;

namespace Circulos.Api.Auth;

public static class Totp
{
    public const int StepSeconds = 30;
    public const int Digits = 6;
    public const int RecoveryCount = 8;

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string NewSecret() => ToBase32(RandomNumberGenerator.GetBytes(20));

    public static string[] NewRecoveryCodes()
    {
        var codes = new string[RecoveryCount];
        for (var i = 0; i < RecoveryCount; i++)
        {
            var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
            codes[i] = $"{raw[..4]}-{raw[4..]}";
        }

        return codes;
    }

    public static string Code(string secret, DateTimeOffset utcNow)
    {
        var counter = utcNow.ToUnixTimeSeconds() / StepSeconds;
        return Compute(secret, counter);
    }

    public static bool Verify(string secret, string code, DateTimeOffset utcNow, int skewSteps = 1)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length != Digits)
        {
            return false;
        }

        var counter = utcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var i = -skewSteps; i <= skewSteps; i++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Compute(secret, counter + i)),
                    Encoding.ASCII.GetBytes(normalized)))
            {
                return true;
            }
        }

        return false;
    }

    public static string OtpauthUri(string email, string secret) =>
        $"otpauth://totp/Circulos:{Uri.EscapeDataString(email)}?secret={secret}&issuer=Circulos&digits={Digits}&period={StepSeconds}";

    public static string NormalizeCode(string code) =>
        new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static string Compute(string secret, long counter)
    {
        var key = FromBase32(secret);
        var payload = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            payload[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(key, payload);
        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString($"D{Digits}");
    }

    public static string ToBase32(byte[] data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    public static byte[] FromBase32(string secret)
    {
        var cleaned = secret.Trim().Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var buffer = 0;
        var bits = 0;
        var output = new List<byte>(cleaned.Length * 5 / 8);
        foreach (var ch in cleaned)
        {
            var index = Alphabet.IndexOf(ch);
            if (index < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
