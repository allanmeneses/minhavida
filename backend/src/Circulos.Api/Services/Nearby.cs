using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public static class Nearby
{
    public const double DefaultRadiusKm = 10;
    public const double MaxRadiusKm = 50;

    public static async Task<List<NearbyHitResponse>> FindAsync(
        AppDbContext db,
        Guid viewerId,
        double lat,
        double lng,
        double radiusKm,
        string? kind,
        CancellationToken cancellationToken)
    {
        var radius = radiusKm <= 0 ? DefaultRadiusKm : Math.Min(radiusKm, MaxRadiusKm);
        var filter = kind?.Trim().ToLowerInvariant();
        if (filter is "person" or "people" or "profile")
        {
            return [];
        }

        if (filter is not null and not "" and not "circle")
        {
            return [];
        }

        var circles = await db.Circles.AsNoTracking()
            .Where(circle => circle.FindableByLocation
                && circle.Status == CircleStatus.Active
                && circle.Latitude != null
                && circle.Longitude != null)
            .ToListAsync(cancellationToken);
        return circles
            .Where(circle => !CircleAccess.HiddenSurprise(circle, viewerId))
            .Select(circle => new
            {
                Circle = circle,
                Km = GeoDistance.Kilometers(lat, lng, circle.Latitude!.Value, circle.Longitude!.Value),
            })
            .Where(item => item.Km <= radius)
            .OrderBy(item => item.Km)
            .Select(item => new NearbyHitResponse(
                "circle",
                item.Circle.Id,
                item.Circle.Name,
                item.Circle.Description,
                GeoDistance.RoundedKm(item.Km)))
            .ToList();
    }
}
