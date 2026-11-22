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
        var hits = new List<NearbyHitResponse>();

        if (filter is null or "" or "person" or "people" or "profile")
        {
            var people = await db.Profiles.AsNoTracking()
                .Where(profile => profile.FindableByLocation
                    && profile.Latitude != null
                    && profile.Longitude != null
                    && profile.UserId != viewerId)
                .ToListAsync(cancellationToken);
            hits.AddRange(people
                .Select(profile => new
                {
                    Profile = profile,
                    Km = GeoDistance.Kilometers(lat, lng, profile.Latitude!.Value, profile.Longitude!.Value),
                })
                .Where(item => item.Km <= radius)
                .OrderBy(item => item.Km)
                .Select(item => new NearbyHitResponse(
                    "person",
                    item.Profile.UserId,
                    item.Profile.DisplayName,
                    item.Profile.IsPublic ? item.Profile.Bio : null,
                    GeoDistance.RoundedKm(item.Km))));
        }

        if (filter is null or "" or "circle")
        {
            var circles = await db.Circles.AsNoTracking()
                .Where(circle => circle.FindableByLocation
                    && circle.Status == CircleStatus.Active
                    && circle.Latitude != null
                    && circle.Longitude != null)
                .ToListAsync(cancellationToken);
            hits.AddRange(circles
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
                    GeoDistance.RoundedKm(item.Km))));
        }

        return hits.OrderBy(item => item.DistanceKm).ToList();
    }
}
