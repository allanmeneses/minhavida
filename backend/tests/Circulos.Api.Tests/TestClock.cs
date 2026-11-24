namespace Circulos.Api.Tests;

internal static class TestClock
{
    public static DateTime OverdueOnThisUtcDay(int minutesAgo = 5)
    {
        var now = DateTime.UtcNow;
        var start = now.AddMinutes(-Math.Max(1, minutesAgo));
        if (start.Date == now.Date)
        {
            return DateTime.SpecifyKind(start, DateTimeKind.Utc);
        }

        var today = now.Date.AddSeconds(1);
        var fallback = today < now ? today : now.AddMilliseconds(-1);
        return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
    }
}
