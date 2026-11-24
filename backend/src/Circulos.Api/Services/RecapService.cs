using System.Text.Json;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class RecapService(AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ProfileRecapResponse> ForProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var flows = new List<RecapFlowDto>();
        var areas = await db.LifeAreas
            .AsNoTracking()
            .Where(area => area.UserId == userId)
            .Select(area => new
            {
                area.Kind,
                area.Name,
                Count = area.Entries.Count,
            })
            .ToListAsync(cancellationToken);

        foreach (var area in areas.Where(item => item.Count > 0))
        {
            var key = area.Kind == LifeAreaKind.Custom ? "custom:" + area.Name : EnumText.Area(area.Kind);
            var label = area.Kind switch
            {
                LifeAreaKind.Vaccine => "Vacinas",
                LifeAreaKind.Reminder => "Lembretes da vida",
                LifeAreaKind.Document => "Documentos da vida",
                LifeAreaKind.Diary => "Diário",
                _ => area.Name,
            };
            flows.Add(new RecapFlowDto(key, label, area.Count));
        }

        var types = await db.RecordTypes.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new { item.Id, item.Name, item.Slug, Count = item.Records.Count })
            .ToListAsync(cancellationToken);
        foreach (var type in types.Where(item => item.Count > 0))
        {
            var key = string.IsNullOrWhiteSpace(type.Slug) ? "type:" + type.Id : type.Slug;
            if (flows.Any(item => item.Key == key || string.Equals(item.Label, type.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            flows.Add(new RecapFlowDto(key, type.Name, type.Count));
        }

        await AddIfUsed(flows, "documents", "Arquivos pessoais", await db.Documents.CountAsync(x => x.UserId == userId, cancellationToken));
        await AddIfUsed(flows, "reminders", "Lembretes", await db.Reminders.CountAsync(x => x.UserId == userId, cancellationToken));
        await AddIfUsed(flows, "shortcuts", "Atalhos", await db.Shortcuts.CountAsync(x => x.UserId == userId, cancellationToken));
        await AddIfUsed(flows, "lists", "Listas", await db.SharedLists.CountAsync(x => x.OwnerUserId == userId && x.CircleId == null, cancellationToken));
        await AddIfUsed(flows, "memories", "Memórias", await db.Memories.CountAsync(x => x.AuthorUserId == userId && x.CircleId == null, cancellationToken));
        await AddIfUsed(flows, "circles", "Círculos", await UsedCircleCountAsync(userId, cancellationToken));
        return new ProfileRecapResponse(flows);
    }

    public async Task<CircleRecapResponse?> ForCircleAsync(Guid userId, Guid circleId, CancellationToken cancellationToken)
    {
        var circle = await db.Circles
            .AsNoTracking()
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == circleId, cancellationToken);
        if (circle is null || CircleAccess.HiddenSurprise(circle, userId))
        {
            return null;
        }

        var member = circle.Members.FirstOrDefault(item => item.UserId == userId);
        if (member is null && !CircleAccess.CanSeeAfterReveal(circle, userId))
        {
            return null;
        }

        var profile = await db.Profiles.AsNoTracking().FirstAsync(item => item.UserId == userId, cancellationToken);
        var flows = new List<RecapFlowDto>();
        var myPosts = await db.CirclePosts
            .AsNoTracking()
            .Where(post => post.CircleId == circleId && post.AuthorUserId == userId)
            .OrderBy(post => post.CreatedAt)
            .Select(post => post.Body)
            .ToListAsync(cancellationToken);
        await AddIfUsed(flows, "posts", "Minhas entradas", myPosts.Count);

        var myMemories = await db.Memories
            .AsNoTracking()
            .Where(memory => memory.CircleId == circleId && memory.AuthorUserId == userId)
            .OrderBy(memory => memory.CreatedAt)
            .Select(memory => memory.Title)
            .ToListAsync(cancellationToken);
        await AddIfUsed(flows, "memories", "Minhas memórias", myMemories.Count);

        var myItems = await db.SharedListItems.CountAsync(
            item => item.AddedByUserId == userId && item.List.CircleId == circleId,
            cancellationToken);
        await AddIfUsed(flows, "lists", "Itens que eu adicionei", myItems);

        var myShared = await db.LifeEntryShares
            .AsNoTracking()
            .Where(share => share.CircleId == circleId && share.LifeEntry.UserId == userId)
            .OrderBy(share => share.LifeEntry.CreatedAt)
            .Select(share => share.LifeEntry.Title)
            .ToListAsync(cancellationToken);
        var myRecords = await db.RecordShares
            .AsNoTracking()
            .Where(share => share.CircleId == circleId && share.Record.UserId == userId)
            .OrderBy(share => share.Record.CreatedAt)
            .Select(share => share.Record.Title)
            .ToListAsync(cancellationToken);
        await AddIfUsed(flows, "shared-entries", "Minhas entradas compartilhadas", myShared.Count + myRecords.Count);

        var titles = myPosts
            .Concat(myMemories)
            .Concat(myShared)
            .Concat(myRecords)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return new CircleRecapResponse(
            circle.Id,
            circle.Name,
            EnumText.Kind(circle.Kind),
            EnumText.Status(circle.Status),
            profile.DisplayName,
            flows,
            titles);
    }

    private Task<int> UsedCircleCountAsync(Guid userId, CancellationToken cancellationToken) =>
        db.CircleMembers.CountAsync(
            member => member.UserId == userId
                && (!member.Circle.IsStarter
                    || member.Circle.Posts.Any()
                    || db.Memories.Any(memory => memory.CircleId == member.CircleId)
                    || db.LifeEntryShares.Any(share => share.CircleId == member.CircleId)
                    || db.RecordShares.Any(share => share.CircleId == member.CircleId)),
            cancellationToken);

    public string Serialize(CircleRecapResponse recap) => JsonSerializer.Serialize(recap, Json);

    public CircleRecapResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<CircleRecapResponse>(json, Json)
        ?? throw new InvalidOperationException("Recap inválido.");

    private static Task AddIfUsed(List<RecapFlowDto> flows, string key, string label, int count)
    {
        if (count > 0)
        {
            flows.Add(new RecapFlowDto(key, label, count));
        }

        return Task.CompletedTask;
    }
}
