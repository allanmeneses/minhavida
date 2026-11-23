using System.Text.Json;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public static class TemplateCatalog
{
    public static readonly Guid ComprasDaSemanaId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid CuidadosDoDiaId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid MateriaisDaEscolaId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid IdeiasDePresenteId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public static readonly Guid TomeiORemedioId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public static readonly Guid CorridaSemanalId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    public static readonly Guid AniversarioAnualId = Guid.Parse("77777777-7777-4777-8777-777777777777");
    public static readonly Guid VacinaAnualId = Guid.Parse("88888888-8888-4888-8888-888888888888");

    public static async Task EnsureSeededAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.RoutineTemplates.Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var template in Seeds())
        {
            if (existing.Contains(template.Id))
            {
                continue;
            }

            db.RoutineTemplates.Add(template);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static string ListPayload(string kind, IReadOnlyList<(string Text, string? Quantity)> items) =>
        JsonSerializer.Serialize(new
        {
            kind,
            items = items.Select(item => new { text = item.Text, quantity = item.Quantity }),
        });

    public static string ReminderPayload(string title, string kind, RecurrenceRule recurrence, string? personName = null) =>
        JsonSerializer.Serialize(new
        {
            title,
            kind,
            recurrence,
            personName,
        });

    private static IEnumerable<RoutineTemplate> Seeds()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        yield return List(
            ComprasDaSemanaId,
            "Compras da semana",
            "Lista pronta de feira: leite, pão, ovos e frutas.",
            "shopping",
            [("Leite", "2 L"), ("Pao", "1"), ("Ovos", "12"), ("Frutas", null)],
            now);
        yield return List(
            CuidadosDoDiaId,
            "Cuidados do dia",
            "Rotina de cuidado: remédio, água e caminhada.",
            "other",
            [("Remédio da manhã", null), ("Beber água", "2 L"), ("Caminhada", "20 min")],
            now);
        yield return List(
            MateriaisDaEscolaId,
            "Materiais da escola",
            "Cadernos, lápis e estojo para o início do período.",
            "materials",
            [("Caderno", "4"), ("Lapis", "12"), ("Estojo", "1")],
            now);
        yield return List(
            IdeiasDePresenteId,
            "Ideias de presente",
            "Lista de desejos para montar o presente.",
            "wishes",
            [("Livro", null), ("Planta", null), ("Chocolate", null)],
            now);
        yield return Reminder(
            TomeiORemedioId,
            "Tomei o remédio",
            "Lembrete diário de medicação, com atalho de um toque.",
            "Tomei o remédio",
            "general",
            new RecurrenceRule { Frequency = "daily", Interval = 1 },
            now);
        yield return Reminder(
            CorridaSemanalId,
            "Corrida da semana",
            "Treino tres vezes por semana.",
            "Corrida",
            "general",
            new RecurrenceRule { Frequency = "weekly", Interval = 1, DaysOfWeek = [1, 3, 5] },
            now);
        yield return Reminder(
            AniversarioAnualId,
            "Aniversário",
            "Aniversário anual com antecedencia de tres dias.",
            "Aniversário",
            "birthday",
            new RecurrenceRule { Frequency = "yearly", Interval = 1 },
            now);
        yield return Reminder(
            VacinaAnualId,
            "Vacina anual",
            "Lembrete anual de vacina, com quando e onde no perfil.",
            "Vacina anual",
            "general",
            new RecurrenceRule { Frequency = "yearly", Interval = 1 },
            now);
    }

    private static RoutineTemplate List(
        Guid id,
        string name,
        string description,
        string kind,
        IReadOnlyList<(string Text, string? Quantity)> items,
        DateTime createdAt) =>
        new()
        {
            Id = id,
            Kind = RoutineTemplateKind.List,
            Name = name,
            Description = description,
            PayloadJson = ListPayload(kind, items),
            Curated = true,
            CreatedAt = createdAt,
        };

    private static RoutineTemplate Reminder(
        Guid id,
        string name,
        string description,
        string title,
        string kind,
        RecurrenceRule recurrence,
        DateTime createdAt) =>
        new()
        {
            Id = id,
            Kind = RoutineTemplateKind.Reminder,
            Name = name,
            Description = description,
            PayloadJson = ReminderPayload(title, kind, recurrence),
            Curated = true,
            CreatedAt = createdAt,
        };
}
