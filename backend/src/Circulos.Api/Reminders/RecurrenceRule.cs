using System.Text.Json;
using System.Text.Json.Serialization;

namespace Circulos.Api.Reminders;

public sealed class RecurrenceRule
{
    public string Frequency { get; set; } = "none";
    public int Interval { get; set; } = 1;
    public int[] DaysOfWeek { get; set; } = [];
    public string MonthlyMode { get; set; } = "date";
    public int? DayOfMonth { get; set; }
    public int? WeekOfMonth { get; set; }
    public DateTime? UntilUtc { get; set; }
    public int? MaxOccurrences { get; set; }

    public static RecurrenceRule Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RecurrenceRule();
        }

        return JsonSerializer.Deserialize<RecurrenceRule>(json, Json) ?? new RecurrenceRule();
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
