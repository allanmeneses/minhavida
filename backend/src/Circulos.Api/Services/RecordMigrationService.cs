using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class RecordMigrationService(AppDbContext db)
{
    public async Task MigrateUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        await RecordGallery.EnsureSeededAsync(db, cancellationToken);
        var areas = await db.LifeAreas.Where(area => area.UserId == userId).OrderBy(area => area.SortOrder).ToListAsync(cancellationToken);
        var typesByArea = await db.RecordTypes
            .Where(type => type.UserId == userId && type.OriginLifeAreaId != null)
            .ToDictionaryAsync(type => type.OriginLifeAreaId!.Value, cancellationToken);

        foreach (var area in areas)
        {
            if (!typesByArea.TryGetValue(area.Id, out var type))
            {
                type = new RecordType
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = area.Name,
                    Description = area.Suggested ? "Exemplo, você pode apagar." : string.Empty,
                    Slug = RecordGallery.SlugForArea(area.Kind),
                    SchemaJson = RecordGallery.SchemaForArea(area.Kind).ToJson(),
                    OriginLifeAreaId = area.Id,
                    OriginTemplateId = GalleryId(area.Kind),
                    CreatedAt = area.CreatedAt,
                    LastUsedAt = area.CreatedAt,
                };
                db.RecordTypes.Add(type);
                typesByArea[area.Id] = type;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var existingEntryIds = await db.LifeRecords
            .Where(item => item.UserId == userId && item.OriginLifeEntryId != null)
            .Select(item => item.OriginLifeEntryId!.Value)
            .ToListAsync(cancellationToken);
        var entries = await db.LifeEntries
            .Include(item => item.Shares)
            .Where(item => item.UserId == userId && !existingEntryIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            if (!typesByArea.TryGetValue(entry.LifeAreaId, out var type))
            {
                continue;
            }

            var schema = RecordSchema.Parse(type.SchemaJson);
            var fields = new Dictionary<string, object?>
            {
                ["title"] = entry.Title,
                ["body"] = entry.Body,
                ["when"] = entry.OccurredOn?.ToString("yyyy-MM-dd"),
                ["where"] = entry.Location,
                ["number"] = entry.NumberValue,
            };
            var record = new LifeRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TypeId = type.Id,
                Title = entry.Title,
                Body = entry.Body,
                OccurredOn = entry.OccurredOn,
                NumberValue = entry.NumberValue,
                PhotoStorageKey = entry.PhotoStorageKey,
                PhotoContentType = entry.PhotoContentType,
                PhotoSizeBytes = entry.PhotoSizeBytes,
                OriginLifeEntryId = entry.Id,
                CreatedAt = entry.CreatedAt,
                UpdatedAt = entry.UpdatedAt,
            };
            RecordFields.ApplyIndexed(record, schema, CloneMap(fields));
            db.LifeRecords.Add(record);
            foreach (var share in entry.Shares)
            {
                db.RecordShares.Add(new RecordShare
                {
                    Id = Guid.NewGuid(),
                    RecordId = record.Id,
                    CircleId = share.CircleId,
                    CreatedAt = share.CreatedAt,
                });
            }

            type.LastUsedAt = Max(type.LastUsedAt, entry.UpdatedAt);
        }

        var existingPages = await db.LifeRecords
            .Where(item => item.UserId == userId && item.OriginDiaryPageId != null)
            .Select(item => item.OriginDiaryPageId!.Value)
            .ToListAsync(cancellationToken);
        var pages = await db.DiaryPages
            .Where(item => item.UserId == userId && !existingPages.Contains(item.Id))
            .ToListAsync(cancellationToken);
        foreach (var page in pages)
        {
            if (!typesByArea.TryGetValue(page.LifeAreaId, out var type))
            {
                continue;
            }

            var schema = RecordSchema.Parse(type.SchemaJson);
            var record = new LifeRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TypeId = type.Id,
                Title = page.Title,
                Body = page.Body,
                OriginDiaryPageId = page.Id,
                CreatedAt = page.CreatedAt,
                UpdatedAt = page.UpdatedAt,
            };
            RecordFields.ApplyIndexed(record, schema, CloneMap(new Dictionary<string, object?>
            {
                ["title"] = page.Title,
                ["body"] = page.Body,
            }));
            db.LifeRecords.Add(record);
            type.LastUsedAt = Max(type.LastUsedAt, page.UpdatedAt);
        }

        var documents = await db.Documents.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        if (documents.Count > 0)
        {
            var docType = await db.RecordTypes.FirstOrDefaultAsync(
                item => item.UserId == userId && item.Slug == "documento",
                cancellationToken);
            if (docType is null)
            {
                docType = new RecordType
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = "Documento",
                    Description = "Exemplo, você pode apagar.",
                    Slug = "documento",
                    SchemaJson = RecordGallery.SchemaForArea(LifeAreaKind.Document).ToJson(),
                    OriginTemplateId = RecordGallery.DocumentoId,
                    CreatedAt = now,
                };
                db.RecordTypes.Add(docType);
                await db.SaveChangesAsync(cancellationToken);
            }

            var existingDocs = await db.LifeRecords
                .Where(item => item.UserId == userId && item.OriginDocumentId != null)
                .Select(item => item.OriginDocumentId!.Value)
                .ToListAsync(cancellationToken);
            var schema = RecordSchema.Parse(docType.SchemaJson);
            foreach (var document in documents.Where(item => !existingDocs.Contains(item.Id)))
            {
                var record = new LifeRecord
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TypeId = docType.Id,
                    Title = document.FileName,
                    FileStorageKey = document.StorageKey,
                    FileContentType = document.ContentType,
                    FileName = document.FileName,
                    FileSizeBytes = document.SizeBytes,
                    OriginDocumentId = document.Id,
                    CreatedAt = document.CreatedAt,
                    UpdatedAt = document.CreatedAt,
                };
                RecordFields.ApplyIndexed(record, schema, CloneMap(new Dictionary<string, object?>
                {
                    ["title"] = document.FileName,
                    ["file"] = document.FileName,
                }));
                db.LifeRecords.Add(record);
                docType.LastUsedAt = Max(docType.LastUsedAt, document.CreatedAt);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static Guid? GalleryId(LifeAreaKind kind) => kind switch
    {
        LifeAreaKind.Vaccine => RecordGallery.VacinasId,
        LifeAreaKind.Diary => RecordGallery.DiarioId,
        LifeAreaKind.Document => RecordGallery.DocumentoId,
        _ => null,
    };

    private static DateTime? Max(DateTime? left, DateTime right) =>
        left is null || left < right ? right : left;

    private static Dictionary<string, JsonElement> CloneMap(Dictionary<string, object?> fields)
    {
        var json = JsonSerializer.Serialize(fields, RecordSchema.Json);
        return RecordFields.Parse(json);
    }
}
