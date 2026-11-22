using Circulos.Api.Auth;

namespace Circulos.Api.Tests;

public sealed class TotpTests
{
    [Fact]
    public void Rfc6238_Sha1_Segundo59()
    {
        var secret = Totp.ToBase32("12345678901234567890"u8.ToArray());
        var at = DateTimeOffset.FromUnixTimeSeconds(59);
        Assert.Equal("287082", Totp.Code(secret, at));
        Assert.True(Totp.Verify(secret, "287082", at));
        Assert.False(Totp.Verify(secret, "000000", at, skewSteps: 0));
    }

    [Fact]
    public void Base32_IdaEVolta()
    {
        var bytes = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
        var encoded = Totp.ToBase32(bytes);
        Assert.Equal(bytes, Totp.FromBase32(encoded));
    }
}
