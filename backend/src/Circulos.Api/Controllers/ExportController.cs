using System.IO.Compression;
using System.Text.Json;
using Circulos.Api.Contracts;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/export")]
public sealed class ExportController(AppDbContext db, IFileStorage storage, TimeProvider clock) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [HttpGet("me")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await db.Users.Include(x => x.Profile).FirstAsync(x => x.Id == userId, cancellationToken);
        var areas = await db.LifeAreas.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var entries = await db.LifeEntries.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var documents = await db.Documents.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var reminders = await db.Reminders.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var shortcuts = await db.Shortcuts.AsNoTracking().Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var lists = await db.SharedLists.AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.OwnerUserId == userId && x.CircleId == null)
            .ToListAsync(cancellationToken);

        var files = new List<(string Path, string Key)>();
        if (user.Profile.PhotoStorageKey is { } photo)
        {
            files.Add(($"files/profile/{user.Profile.PhotoFileName ?? "foto.jpg"}", photo));
        }

        foreach (var document in documents)
        {
            files.Add(($"files/documents/{document.Id:N}-{document.FileName}", document.StorageKey));
        }

        foreach (var entry in entries.Where(x => x.PhotoStorageKey is not null))
        {
            files.Add(($"files/entries/{entry.Id:N}.jpg", entry.PhotoStorageKey!));
        }

        var payload = new
        {
            kind = "profile",
            exportedAt = clock.GetUtcNow().UtcDateTime,
            profile = new
            {
                user.Email,
                user.Profile.DisplayName,
                user.Profile.BirthDate,
                user.Profile.Phone,
                user.Profile.SecondaryEmail,
                user.Profile.IsPublic,
            },
            plan = new { user.Plan, user.StorageUsedBytes, limit = PlanLimits.StorageLimit(user.Plan) },
            areas = areas.Select(x => new { x.Id, x.Name, kind = EnumText.Area(x.Kind), x.Suggested }),
            entries = entries.Select(x => new
            {
                x.Id,
                x.LifeAreaId,
                x.Title,
                x.Body,
                x.NumberValue,
                x.OccurredOn,
                x.Location,
                hasPhoto = x.PhotoStorageKey is not null,
                x.CreatedAt,
            }),
            documents = documents.Select(x => new { x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAt }),
            reminders = reminders.Select(x => new { x.Id, x.Title, x.NextFireAt, x.IsPaused, x.PersonName, x.GiftIdeasJson }),
            shortcuts = shortcuts.Select(x => new { x.Id, x.Label, x.Color, x.IsActive }),
            lists = lists.Select(x => new
            {
                x.Id,
                x.Name,
                kind = EnumText.ListKind(x.Kind),
                items = x.Items.Select(i => new { i.Id, i.Text, i.Quantity, i.Note, i.IsDone }),
            }),
        };

        return File(await ZipAsync(payload, files, cancellationToken), "application/zip", "circulos-perfil.zip");
    }

    [HttpGet("circles/{id:guid}")]
    public async Task<IActionResult> Circle(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var circle = await db.Circles
            .Include(x => x.Members)
            .Include(x => x.Posts)
            .Include(x => x.Logs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (circle is null || HiddenSurprise(circle, userId) || circle.Members.All(x => x.UserId != userId))
        {
            return NotFound(new ErrorResponse("Circulo nao encontrado."));
        }

        var names = await db.Profiles.AsNoTracking()
            .Where(x => circle.Members.Select(m => m.UserId).Contains(x.UserId))
            .ToDictionaryAsync(x => x.UserId, x => x.DisplayName, cancellationToken);
        var lists = await db.SharedLists
            .Include(x => x.Items)
            .Include(x => x.Grants)
            .Where(x => x.CircleId == id)
            .ToListAsync(cancellationToken);
        var visibleLists = lists.Where(list => !HideWishList(circle, userId, list.Kind)).ToList();

        var files = new List<(string Path, string Key)>();
        foreach (var post in circle.Posts.Where(x => x.PhotoStorageKey is not null))
        {
            files.Add(($"files/posts/{post.Id:N}.jpg", post.PhotoStorageKey!));
        }

        var payload = new
        {
            kind = "circle",
            exportedAt = clock.GetUtcNow().UtcDateTime,
            circle = new
            {
                circle.Id,
                circle.Name,
                circle.Description,
                visibility = EnumText.Visibility(circle.Visibility),
                kind = EnumText.Kind(circle.Kind),
                status = EnumText.Status(circle.Status),
            },
            members = circle.Members.Select(x => new
            {
                x.UserId,
                name = names.GetValueOrDefault(x.UserId, "Pessoa"),
                role = EnumText.Role(x.Role),
            }),
            posts = circle.Posts.Select(x => new { x.Id, x.Body, x.AuthorUserId, x.CreatedAt, hasPhoto = x.PhotoStorageKey is not null }),
            log = circle.Logs.Select(x => new { x.Id, x.Action, x.Detail, x.ActorUserId, x.CreatedAt }),
            lists = visibleLists.Select(x => new
            {
                x.Id,
                x.Name,
                kind = EnumText.ListKind(x.Kind),
                items = x.Items.Select(i => new { i.Id, i.Text, i.Quantity, i.Note, i.IsDone, i.AddedByUserId }),
            }),
        };

        return File(await ZipAsync(payload, files, cancellationToken), "application/zip", $"circulos-{circle.Id:N}.zip");
    }

    private async Task<byte[]> ZipAsync(object payload, IReadOnlyList<(string Path, string Key)> files, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var json = zip.CreateEntry("dados.json");
            await using (var stream = json.Open())
            {
                await JsonSerializer.SerializeAsync(stream, payload, Json, cancellationToken);
            }

            foreach (var file in files)
            {
                try
                {
                    await using var stored = await storage.OpenReadAsync(file.Key, cancellationToken);
                    var entry = zip.CreateEntry(file.Path);
                    await using var dest = entry.Open();
                    await stored.CopyToAsync(dest, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    // arquivo ja apagado do disco; o JSON segue com o rastro
                }
            }
        }

        return output.ToArray();
    }

    private static bool HiddenSurprise(Circle circle, Guid userId) =>
        circle.IsSurprise && circle.RevealedAt is null && circle.SurpriseSubjectUserId == userId;

    private static bool HideWishList(Circle circle, Guid userId, SharedListKind kind) =>
        kind == SharedListKind.Wishes && HiddenSurprise(circle, userId);
}
