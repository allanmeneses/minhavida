namespace Circulos.Api.Services;

public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0;

    public static double Kilometers(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return EarthRadiusKm * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static double RoundedKm(double kilometers) => Math.Round(kilometers, 1, MidpointRounding.AwayFromZero);

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
