namespace Circulos.Api.Reminders;

public static class RecurrenceCalculator
{
    public static DateTime? NextOnOrAfter(RecurrenceRule rule, DateTime startUtc, DateTime afterUtc, int completedCount)
    {
        if (rule.MaxOccurrences is int max && completedCount >= max)
        {
            return null;
        }

        if (string.Equals(rule.Frequency, "none", StringComparison.OrdinalIgnoreCase))
        {
            return startUtc >= afterUtc ? startUtc : null;
        }

        var interval = Math.Max(1, rule.Interval);
        var cursor = startUtc;
        var guard = 0;
        while (cursor < afterUtc && guard < 20000)
        {
            cursor = Advance(rule, startUtc, cursor, interval);
            guard++;
            if (rule.UntilUtc is { } until && cursor > until)
            {
                return null;
            }
        }

        if (rule.UntilUtc is { } end && cursor > end)
        {
            return null;
        }

        if (rule.MaxOccurrences is int limit)
        {
            var remaining = limit - completedCount;
            if (remaining <= 0)
            {
                return null;
            }
        }

        return cursor;
    }

    public static bool StillRelevant(RecurrenceRule rule, DateTime startUtc, DateTime nextFireAt, DateTime nowUtc)
    {
        if (string.Equals(rule.Frequency, "none", StringComparison.OrdinalIgnoreCase))
        {
            return nextFireAt >= nowUtc.Date;
        }

        return NextOnOrAfter(rule, startUtc, nowUtc, 0) is not null;
    }

    public static IReadOnlyList<DateTime> OccurrencesInRange(
        RecurrenceRule rule,
        DateTime startUtc,
        DateTime fromUtc,
        DateTime toUtc,
        int maxOccurrencesShown = 400)
    {
        if (toUtc < fromUtc)
        {
            return [];
        }

        var items = new List<DateTime>();
        var cursor = fromUtc;
        var count = 0;
        while (items.Count < maxOccurrencesShown)
        {
            var next = NextOnOrAfter(rule, startUtc, cursor, count);
            if (next is null || next > toUtc)
            {
                break;
            }

            if (next >= fromUtc)
            {
                items.Add(next.Value);
            }

            cursor = next.Value.AddSeconds(1);
            count++;
        }

        return items;
    }

    private static DateTime Advance(RecurrenceRule rule, DateTime startUtc, DateTime current, int interval)
    {
        var frequency = rule.Frequency.ToLowerInvariant();
        return frequency switch
        {
            "daily" or "n_days" => current.AddDays(interval),
            "weekly" or "n_weeks" => AdvanceWeekly(rule, current, interval),
            "monthly" => AdvanceMonthly(rule, startUtc, current, interval),
            "yearly" => AdvanceYearly(rule, startUtc, current, interval),
            _ => current.AddDays(interval),
        };
    }

    private static DateTime AdvanceWeekly(RecurrenceRule rule, DateTime current, int interval)
    {
        var days = rule.DaysOfWeek is { Length: > 0 }
            ? rule.DaysOfWeek.Select(d => (DayOfWeek)(((d % 7) + 7) % 7)).Distinct().OrderBy(d => d).ToArray()
            : [current.DayOfWeek];

        var probe = current.AddDays(1);
        var weekLimit = current.AddDays(7 * interval);
        while (probe <= weekLimit.AddDays(7))
        {
            if (days.Contains(probe.DayOfWeek) && probe > current)
            {
                if (probe.DayOfWeek <= current.DayOfWeek)
                {
                    var weeks = interval;
                    return current.Date.AddDays(((int)probe.DayOfWeek - (int)current.DayOfWeek) + (7 * weeks)).Add(current.TimeOfDay);
                }

                return probe.Date.Add(current.TimeOfDay);
            }

            probe = probe.AddDays(1);
        }

        return current.AddDays(7 * interval);
    }

    private static DateTime AdvanceMonthly(RecurrenceRule rule, DateTime startUtc, DateTime current, int interval)
    {
        var nextMonth = current.AddMonths(interval);
        if (string.Equals(rule.MonthlyMode, "position", StringComparison.OrdinalIgnoreCase)
            && rule.WeekOfMonth is int week)
        {
            var dow = startUtc.DayOfWeek;
            return NthWeekday(nextMonth.Year, nextMonth.Month, dow, week, startUtc.TimeOfDay);
        }

        var day = rule.DayOfMonth ?? startUtc.Day;
        var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        var clamped = Math.Min(day, daysInMonth);
        return new DateTime(nextMonth.Year, nextMonth.Month, clamped, startUtc.Hour, startUtc.Minute, startUtc.Second, DateTimeKind.Utc);
    }

    private static DateTime AdvanceYearly(RecurrenceRule rule, DateTime startUtc, DateTime current, int interval)
    {
        var next = current.AddYears(interval);
        if (string.Equals(rule.MonthlyMode, "position", StringComparison.OrdinalIgnoreCase)
            && rule.WeekOfMonth is int week)
        {
            return NthWeekday(next.Year, startUtc.Month, startUtc.DayOfWeek, week, startUtc.TimeOfDay);
        }

        var day = Math.Min(startUtc.Day, DateTime.DaysInMonth(next.Year, startUtc.Month));
        return new DateTime(next.Year, startUtc.Month, day, startUtc.Hour, startUtc.Minute, startUtc.Second, DateTimeKind.Utc);
    }

    private static DateTime NthWeekday(int year, int month, DayOfWeek dayOfWeek, int week, TimeSpan time)
    {
        if (week == -1)
        {
            var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), time.Hours, time.Minutes, time.Seconds, DateTimeKind.Utc);
            while (last.DayOfWeek != dayOfWeek)
            {
                last = last.AddDays(-1);
            }

            return last;
        }

        var date = new DateTime(year, month, 1, time.Hours, time.Minutes, time.Seconds, DateTimeKind.Utc);
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(1);
        }

        date = date.AddDays(7 * Math.Max(0, week - 1));
        if (date.Month != month)
        {
            date = date.AddDays(-7);
        }

        return date;
    }
}
