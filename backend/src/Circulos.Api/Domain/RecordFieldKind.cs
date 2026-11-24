namespace Circulos.Api.Domain;

public static class RecordFieldKind
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Date = "date";
    public const string Place = "place";
    public const string Bool = "bool";
    public const string Select = "select";
    public const string Photo = "photo";
    public const string File = "file";

    public static readonly string[] All =
    [
        Text, Number, Date, Place, Bool, Select, Photo, File,
    ];

    public static bool IsKnown(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && All.Contains(kind.Trim().ToLowerInvariant());

    public static string Normalize(string? kind)
    {
        var value = (kind ?? Text).Trim().ToLowerInvariant();
        return IsKnown(value) ? value : Text;
    }
}
