using Circulos.Api.Data;
using Circulos.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public static class RecordGallery
{
    public static readonly Guid VacinasId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    public static readonly Guid DiarioId = Guid.Parse("a2222222-2222-4222-8222-222222222222");
    public static readonly Guid DocumentoId = Guid.Parse("a3333333-3333-4333-8333-333333333333");
    public static readonly Guid RemedioId = Guid.Parse("a4444444-4444-4444-8444-444444444444");
    public static readonly Guid PetId = Guid.Parse("a5555555-5555-4555-8555-555555555555");
    public static readonly Guid CarroId = Guid.Parse("a6666666-6666-4666-8666-666666666666");
    public static readonly Guid LivroId = Guid.Parse("a7777777-7777-4777-8777-777777777777");
    public static readonly Guid CompraId = Guid.Parse("a8888888-8888-4888-8888-888888888888");

    public static async Task EnsureSeededAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.RecordTypes.Where(item => item.UserId == null).Select(item => item.Id).ToListAsync(cancellationToken);
        foreach (var template in Seeds())
        {
            if (existing.Contains(template.Id))
            {
                continue;
            }

            db.RecordTypes.Add(template);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static RecordSchema SchemaForArea(LifeAreaKind kind) => kind switch
    {
        LifeAreaKind.Vaccine => Schema(
            RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
            RecordField.Create("when", "Quando", RecordFieldKind.Date, 1, true),
            RecordField.Create("where", "Onde", RecordFieldKind.Place, 2),
            RecordField.Create("lote", "Lote", RecordFieldKind.Text, 3),
            RecordField.Create("photo", "Foto", RecordFieldKind.Photo, 4)),
        LifeAreaKind.Diary => Schema(
            RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
            RecordField.Create("body", "Texto", RecordFieldKind.Text, 1),
            RecordField.Create("when", "Quando", RecordFieldKind.Date, 2)),
        LifeAreaKind.Document => Schema(
            RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
            RecordField.Create("file", "Arquivo", RecordFieldKind.File, 1),
            RecordField.Create("when", "Quando", RecordFieldKind.Date, 2)),
        LifeAreaKind.Reminder => Schema(
            RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
            RecordField.Create("when", "Quando", RecordFieldKind.Date, 1),
            RecordField.Create("body", "Detalhe", RecordFieldKind.Text, 2)),
        _ => Schema(
            RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
            RecordField.Create("when", "Quando", RecordFieldKind.Date, 1),
            RecordField.Create("where", "Onde", RecordFieldKind.Place, 2),
            RecordField.Create("number", "Número", RecordFieldKind.Number, 3),
            RecordField.Create("body", "Texto", RecordFieldKind.Text, 4)),
    };

    public static string SlugForArea(LifeAreaKind kind) => kind switch
    {
        LifeAreaKind.Vaccine => "vacinas",
        LifeAreaKind.Diary => "diario",
        LifeAreaKind.Document => "documento",
        LifeAreaKind.Reminder => "lembretes",
        _ => "livre",
    };

    public static IReadOnlyList<RecordType> Seeds()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return
        [
            Gallery(VacinasId, "Vacinas", "vacinas", "Exemplo: vacina, quando e onde. Copie se fizer sentido para você.", SchemaForArea(LifeAreaKind.Vaccine), now),
            Gallery(DiarioId, "Diário", "diario", "Exemplo: um parágrafo do dia. Busca por palavra-chave. Sem versão.", SchemaForArea(LifeAreaKind.Diary), now),
            Gallery(DocumentoId, "Documento", "documento", "Exemplo: nome do que você guarda para acesso rápido.", SchemaForArea(LifeAreaKind.Document), now),
            Gallery(
                RemedioId,
                "Remédio",
                "remedio",
                "Anote o que tomou, a dose e se já foi feito.",
                Schema(
                    RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
                    RecordField.Create("when", "Quando", RecordFieldKind.Date, 1),
                    RecordField.Create("dose", "Dose", RecordFieldKind.Number, 2),
                    RecordField.Create("done", "Tomado", RecordFieldKind.Bool, 3)),
                now),
            Gallery(
                PetId,
                "Pet",
                "pet",
                "Peso, consulta ou vacina do pet. O perfil vivo continua sendo quem é.",
                Schema(
                    RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
                    RecordField.Create("when", "Quando", RecordFieldKind.Date, 1),
                    RecordField.Create("peso", "Peso", RecordFieldKind.Number, 2),
                    RecordField.Create("where", "Onde", RecordFieldKind.Place, 3)),
                now),
            Gallery(
                CarroId,
                "Carro",
                "carro",
                "Revisão, troca de óleo ou quilometragem.",
                Schema(
                    RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
                    RecordField.Create("km", "Quilometragem", RecordFieldKind.Number, 1),
                    RecordField.Create("when", "Quando", RecordFieldKind.Date, 2),
                    RecordField.Create("where", "Onde", RecordFieldKind.Place, 3)),
                now),
            Gallery(
                LivroId,
                "Livro",
                "livro",
                "O que você leu, quem escreveu e quando terminou.",
                Schema(
                    RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
                    RecordField.Create("autor", "Autor", RecordFieldKind.Text, 1),
                    RecordField.Create("when", "Quando", RecordFieldKind.Date, 2),
                    RecordField.Create("nota", "Nota", RecordFieldKind.Number, 3)),
                now),
            Gallery(
                CompraId,
                "Compra",
                "compra",
                "Uma compra avulsa, com valor e lugar. Lista de supermercado continua sendo lista.",
                Schema(
                    RecordField.Create("title", "Título", RecordFieldKind.Text, 0, true),
                    RecordField.Create("valor", "Valor", RecordFieldKind.Number, 1),
                    RecordField.Create("when", "Quando", RecordFieldKind.Date, 2),
                    RecordField.Create("where", "Onde", RecordFieldKind.Place, 3)),
                now),
        ];
    }

    private static RecordSchema Schema(params RecordField[] fields) => new() { Fields = [.. fields] };

    private static RecordType Gallery(Guid id, string name, string slug, string description, RecordSchema schema, DateTime now) =>
        new()
        {
            Id = id,
            UserId = null,
            Name = name,
            Slug = slug,
            Description = description,
            SchemaJson = schema.ToJson(),
            CreatedAt = now,
        };
}
