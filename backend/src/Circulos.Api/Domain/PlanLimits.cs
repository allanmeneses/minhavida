namespace Circulos.Api.Domain;

public static class PlanLimits
{
    public const int FreeCircleLimit = 3;
    public const int FreeShortcutLimit = 5;
    public const int PaidShortcutLimit = 20;
    public const long FreeStorageBytes = 100L * 1024 * 1024;
    public const long PaidStorageBytes = 1024L * 1024 * 1024;
    public const double WarningRatio = 0.90;

    public static bool BenefitsActive(User user, DateTime utcNow)
    {
        if (user.Plan != PlanKind.Paid)
        {
            return false;
        }

        return user.SubscriptionStatus switch
        {
            SubscriptionStatus.Active => true,
            SubscriptionStatus.Cancelled => user.PaidUntil is { } until && until > utcNow,
            _ => false,
        };
    }

    public static int? CircleLimit(bool paid) => paid ? null : FreeCircleLimit;

    public static int? CircleLimit(PlanKind plan) => CircleLimit(plan == PlanKind.Paid);

    public static long StorageLimit(bool paid) => paid ? PaidStorageBytes : FreeStorageBytes;

    public static long StorageLimit(PlanKind plan) => StorageLimit(plan == PlanKind.Paid);

    public static int ShortcutLimit(bool paid) => paid ? PaidShortcutLimit : FreeShortcutLimit;

    public static bool ShouldWarn(long usedBytes, long limitBytes) =>
        limitBytes > 0 && usedBytes >= (long)(limitBytes * WarningRatio);
}
