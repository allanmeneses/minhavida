using System.Globalization;
using System.Text;

namespace Circulos.Api.Reminders;

public static class IcsCalendar
{
    public static string Build(string calendarName, IReadOnlyList<IcsEvent> events)
    {
        var text = new StringBuilder();
        text.AppendLine("BEGIN:VCALENDAR");
        text.AppendLine("VERSION:2.0");
        text.AppendLine("PRODID:-//Circulos//Lembretes//PT");
        text.AppendLine("CALSCALE:GREGORIAN");
        text.AppendLine("METHOD:PUBLISH");
        Fold(text, "X-WR-CALNAME:" + Escape(calendarName));
        foreach (var item in events)
        {
            text.AppendLine("BEGIN:VEVENT");
            Fold(text, "UID:" + item.Uid);
            Fold(text, "DTSTAMP:" + Format(item.StampUtc));
            Fold(text, "DTSTART:" + Format(item.StartUtc));
            Fold(text, "SUMMARY:" + Escape(item.Summary));
            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                Fold(text, "DESCRIPTION:" + Escape(item.Description));
            }

            text.AppendLine("END:VEVENT");
        }

        text.AppendLine("END:VCALENDAR");
        return text.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    public static string Format(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    public static string Escape(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");

    private static void Fold(StringBuilder text, string line)
    {
        if (line.Length <= 75)
        {
            text.AppendLine(line);
            return;
        }

        text.AppendLine(line[..75]);
        var rest = line[75..];
        while (rest.Length > 74)
        {
            text.AppendLine(" " + rest[..74]);
            rest = rest[74..];
        }

        text.AppendLine(" " + rest);
    }
}

public sealed record IcsEvent(string Uid, DateTime StampUtc, DateTime StartUtc, string Summary, string? Description);
