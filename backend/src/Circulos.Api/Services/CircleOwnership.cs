using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public static class CircleOwnership
{
    public static void MarkLost(Circle circle, DateTime now)
    {
        circle.OwnerUserId = null;
        circle.Owner = null;
        circle.OwnerLostAt = now;
        circle.OwnershipNextCheckAt = now.AddDays(OwnershipOfferService.WaitDays);
        if (circle.Status == CircleStatus.LockedNoOwner)
        {
            circle.Status = CircleStatus.Active;
        }
    }

    public static async Task AssignAsync(
        AppDbContext db,
        Circle circle,
        Guid winnerId,
        DateTime now,
        string action,
        string detail,
        CancellationToken cancellationToken)
    {
        var member = circle.Members.FirstOrDefault(item => item.UserId == winnerId)
            ?? throw new InvalidOperationException("A pessoa precisa ser membro do circulo.");
        var winner = await db.Users.FirstAsync(item => item.Id == winnerId, cancellationToken);
        var previousOwnerId = circle.OwnerUserId;
        if (previousOwnerId is Guid previous && previous != winnerId)
        {
            var previousMember = circle.Members.FirstOrDefault(item => item.UserId == previous);
            if (previousMember is not null)
            {
                previousMember.Role = CircleRole.Admin;
            }

            var previousUser = await db.Users.FirstAsync(item => item.Id == previous, cancellationToken);
            if (!circle.IsStarter && previousUser.OwnedCircleCount > 0)
            {
                previousUser.OwnedCircleCount -= 1;
            }
        }

        circle.OwnerUserId = winnerId;
        circle.Owner = winner;
        circle.Status = CircleStatus.Active;
        circle.OwnerElectedAt = now;
        circle.OwnerLostAt = null;
        circle.OwnershipNextCheckAt = null;
        member.Role = CircleRole.Owner;
        if (!circle.IsStarter && previousOwnerId != winnerId)
        {
            winner.OwnedCircleCount += 1;
        }

        var open = await db.OwnershipOffers
            .Where(offer => offer.CircleId == circle.Id && offer.Status == OwnershipOfferStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var offer in open)
        {
            offer.Status = offer.UserId == winnerId
                ? OwnershipOfferStatus.Accepted
                : OwnershipOfferStatus.Cancelled;
            offer.RespondedAt = now;
        }

        db.CircleLogs.Add(new CircleLog
        {
            Id = Guid.NewGuid(),
            CircleId = circle.Id,
            ActorUserId = winnerId,
            Action = action,
            Detail = detail,
            CreatedAt = now,
        });
    }
}
