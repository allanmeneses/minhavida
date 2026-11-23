using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public static class StreakService
{
    public static int Touch(User user, DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc);
        if (user.LastRoutineDate == today)
        {
            return user.RoutineStreakDays;
        }

        if (user.LastRoutineDate == today.AddDays(-1))
        {
            user.RoutineStreakDays += 1;
        }
        else
        {
            user.RoutineStreakDays = 1;
        }

        user.LastRoutineDate = today;
        return user.RoutineStreakDays;
    }
}
