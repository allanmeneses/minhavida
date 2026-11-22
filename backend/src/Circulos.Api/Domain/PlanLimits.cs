namespace Circulos.Api.Domain;

public static class PlanLimits
{
    public const int FreeCircleLimit = 3;
    public const long FreeStorageBytes = 100L * 1024 * 1024;
    public const long PaidStorageBytes = 1024L * 1024 * 1024;
    public const double WarningRatio = 0.90;

    public static int? CircleLimit(PlanKind plan) =>
        plan == PlanKind.Paid ? null : FreeCircleLimit;

    public static long StorageLimit(PlanKind plan) =>
        plan == PlanKind.Paid ? PaidStorageBytes : FreeStorageBytes;

    public static bool ShouldWarn(long usedBytes, long limitBytes) =>
        limitBytes > 0 && usedBytes >= (long)(limitBytes * WarningRatio);
}
