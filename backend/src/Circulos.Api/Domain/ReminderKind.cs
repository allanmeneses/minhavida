namespace Circulos.Api.Domain;

public enum ReminderKind
{
    General = 0,
    Birthday = 1,
}

public enum ShortcutActionKind
{
    MarkDone = 0,
    Start = 1,
    Note = 2,
}

public static class ShortcutPalette
{
    public const string MarkDone = "#C45C26";
    public const string Start = "#1F6F66";
    public const string Note = "#4A6FA5";

    public static string ColorFor(ShortcutActionKind kind) => kind switch
    {
        ShortcutActionKind.Start => Start,
        ShortcutActionKind.Note => Note,
        _ => MarkDone,
    };

    public static ShortcutActionKind Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "start" or "iniciar" => ShortcutActionKind.Start,
        "note" or "nota" => ShortcutActionKind.Note,
        _ => ShortcutActionKind.MarkDone,
    };

    public static string Text(ShortcutActionKind kind) => kind switch
    {
        ShortcutActionKind.Start => "start",
        ShortcutActionKind.Note => "note",
        _ => "done",
    };
}
