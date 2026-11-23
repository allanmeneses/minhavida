using System.Text.RegularExpressions;
using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public static partial class LocationPrivacy
{
    [GeneratedRegex(@"-?\d{1,3}\.\d+", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalCoordinate();

    public static void ClearUserCoordinates(Profile profile)
    {
        profile.Latitude = null;
        profile.Longitude = null;
    }

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var withoutQuery = text
            .Replace("lat=", "pos=", StringComparison.OrdinalIgnoreCase)
            .Replace("lng=", "pos=", StringComparison.OrdinalIgnoreCase)
            .Replace("longitude=", "pos=", StringComparison.OrdinalIgnoreCase)
            .Replace("latitude=", "pos=", StringComparison.OrdinalIgnoreCase);
        return DecimalCoordinate().Replace(withoutQuery, "[redacted]");
    }

    public static bool ContainsCoordinate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.Contains("lat=", StringComparison.OrdinalIgnoreCase)
            || text.Contains("lng=", StringComparison.OrdinalIgnoreCase)
            || text.Contains("latitude", StringComparison.OrdinalIgnoreCase)
            || text.Contains("longitude", StringComparison.OrdinalIgnoreCase)
            || DecimalCoordinate().IsMatch(text);
    }
}
