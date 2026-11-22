using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class AccountDeletionService(
    AppDbContext db,
    IFileStorage storage,
    IEmailSender email)
{
    public async Task DeleteAsync(User user, CancellationToken cancellationToken)
    {
        var keys = await CollectKeysAsync(user.Id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await UserRowLock.AcquireAsync(db, user.Id, cancellationToken);

        var owned = await db.Circles
            .Include(circle => circle.Members)
            .Where(circle => circle.OwnerUserId == user.Id)
            .ToListAsync(cancellationToken);

        foreach (var circle in owned)
        {
            var successor = circle.SuccessorUserId is Guid successorId
                ? circle.Members.FirstOrDefault(member => member.UserId == successorId && member.UserId != user.Id)
                : null;
            var admin = successor ?? circle.Members
                .Where(member => member.UserId != user.Id && member.Role >= CircleRole.Admin)
                .OrderByDescending(member => member.Role)
                .ThenBy(member => member.JoinedAt)
                .FirstOrDefault();

            if (admin is null)
            {
                circle.OwnerUserId = null;
                circle.Status = CircleStatus.LockedNoOwner;
                db.CircleLogs.Add(Log(circle.Id, null, "locked", "Sem dono ativo. O circulo espera um responsavel."));
            }
            else
            {
                var successorUser = await db.Users.FirstAsync(candidate => candidate.Id == admin.UserId, cancellationToken);
                circle.Owner = successorUser;
                circle.OwnerUserId = successorUser.Id;
                admin.Role = CircleRole.Owner;
                db.CircleLogs.Add(Log(circle.Id, admin.UserId, "transferred", "Dono anterior saiu. O circulo passou para um administrador."));
                successorUser.OwnedCircleCount += 1;
            }
        }

        user.OwnedCircleCount = 0;

        var logs = await db.CircleLogs.Where(log => log.ActorUserId == user.Id).ToListAsync(cancellationToken);
        foreach (var log in logs)
        {
            log.ActorUserId = null;
        }

        var posts = await db.CirclePosts.Where(post => post.AuthorUserId == user.Id).ToListAsync(cancellationToken);
        foreach (var post in posts)
        {
            post.AuthorUserId = null;
        }

        var circleMemories = await db.Memories.Where(memory => memory.AuthorUserId == user.Id && memory.CircleId != null).ToListAsync(cancellationToken);
        db.Memories.RemoveRange(circleMemories);

        var soloLists = await db.SharedLists
            .Where(list => list.OwnerUserId == user.Id && list.CircleId == null)
            .ToListAsync(cancellationToken);
        db.SharedLists.RemoveRange(soloLists);

        var remainingLists = await db.SharedLists.Where(list => list.OwnerUserId == user.Id).ToListAsync(cancellationToken);
        foreach (var list in remainingLists)
        {
            list.OwnerUserId = null;
        }

        var grants = await db.SharedListGrants.Where(grant => grant.UserId == user.Id).ToListAsync(cancellationToken);
        db.SharedListGrants.RemoveRange(grants);

        var addedItems = await db.SharedListItems.Where(item => item.AddedByUserId == user.Id).ToListAsync(cancellationToken);
        foreach (var item in addedItems)
        {
            item.AddedByUserId = null;
        }

        var doneItems = await db.SharedListItems.Where(item => item.DoneByUserId == user.Id).ToListAsync(cancellationToken);
        foreach (var item in doneItems)
        {
            item.DoneByUserId = null;
        }

        var memberships = await db.CircleMembers.Where(member => member.UserId == user.Id).ToListAsync(cancellationToken);
        db.CircleMembers.RemoveRange(memberships);

        var requests = await db.CircleJoinRequests.Where(request => request.UserId == user.Id).ToListAsync(cancellationToken);
        db.CircleJoinRequests.RemoveRange(requests);

        await db.SaveChangesAsync(cancellationToken);
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var key in keys)
        {
            await storage.DeleteAsync(key, cancellationToken);
        }

        await email.SendAsync(
            user.Email,
            "Conta do Circulos encerrada",
            "Sua conta foi excluida. Os dados que eram so seus foram apagados. Este e-mail nao reativa a conta.",
            cancellationToken);
    }

    private async Task<List<string>> CollectKeysAsync(Guid userId, CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        keys.AddRange(await db.Documents.Where(x => x.UserId == userId && x.StorageKey != "").Select(x => x.StorageKey).ToListAsync(cancellationToken));
        keys.AddRange(await db.Profiles.Where(x => x.UserId == userId && x.PhotoStorageKey != null).Select(x => x.PhotoStorageKey!).ToListAsync(cancellationToken));
        keys.AddRange(await db.LifeEntries.Where(x => x.UserId == userId && x.PhotoStorageKey != null).Select(x => x.PhotoStorageKey!).ToListAsync(cancellationToken));
        keys.AddRange(await db.Reminders.Where(x => x.UserId == userId && x.PhotoStorageKey != null).Select(x => x.PhotoStorageKey!).ToListAsync(cancellationToken));
        keys.AddRange(await db.Memories.Where(x => x.AuthorUserId == userId && x.PhotoStorageKey != null).Select(x => x.PhotoStorageKey!).ToListAsync(cancellationToken));
        keys.AddRange(await db.LivingProfiles.Where(x => x.OwnerUserId == userId && x.PhotoStorageKey != null).Select(x => x.PhotoStorageKey!).ToListAsync(cancellationToken));
        return keys;
    }

    private static CircleLog Log(Guid circleId, Guid? actorId, string action, string detail) =>
        new()
        {
            Id = Guid.NewGuid(),
            CircleId = circleId,
            ActorUserId = actorId,
            Action = action,
            Detail = detail,
            CreatedAt = DateTime.UtcNow,
        };
}
