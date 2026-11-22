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
                LifeAreaKind.Diary => "Diario",
                _ => area.Name,
            };
            flows.Add(new RecapFlowDto(key, label, area.Count));
        }

        await AddIfUsed(flows, "documents", "Documentos", await db.Documents.CountAsync(x => x.UserId == userId, cancellationToken));
        await AddIfUsed(flows, "reminders", "Lembretes", await db.Reminders.CountAsync(x => x.UserId == userId, cancellationToken));
        await AddIfUsed(flows, "shortcuts", "Atalhos", await db.Shortcuts.CountAsync(x => x.UserId == userId, cancellationToken));
        await AddIfUsed(flows, "lists", "Listas", await db.SharedLists.CountAsync(x => x.OwnerUserId == userId && x.CircleId == null, cancellationToken));
        await AddIfUsed(flows, "memories", "Memorias", await db.Memories.CountAsync(x => x.AuthorUserId == userId && x.CircleId == null, cancellationToken));
        await AddIfUsed(flows, "circles", "Circulos", await db.CircleMembers.CountAsync(x => x.UserId == userId, cancellationToken));
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
        await AddIfUsed(flows, "memories", "Minhas memorias", myMemories.Count);

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
        await AddIfUsed(flows, "shared-entries", "Minhas entradas compartilhadas", myShared.Count);

        var titles = myPosts
            .Concat(myMemories)
            .Concat(myShared)
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

    public string Serialize(CircleRecapResponse recap) => JsonSerializer.Serialize(recap, Json);

    public CircleRecapResponse Deserialize(string json) =>
        JsonSerializer.Deserialize<CircleRecapResponse>(json, Json)
        ?? throw new InvalidOperationException("Recap invalido.");

    private static Task AddIfUsed(List<RecapFlowDto> flows, string key, string label, int count)
    {
        if (count > 0)
        {
            flows.Add(new RecapFlowDto(key, label, count));
        }

        return Task.CompletedTask;
    }
}
