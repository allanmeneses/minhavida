namespace Circulos.Api.Services;

public static class ListPaging
{
    public const int DefaultTake = 20;
    public const int MaxTake = 50;

    public static (int Skip, int Take) Clamp(int? skip, int? take)
    {
        var safeSkip = Math.Max(0, skip ?? 0);
        if (take is null)
        {
            return (safeSkip, int.MaxValue);
        }

        return (safeSkip, Math.Clamp(take.Value, 1, MaxTake));
    }
}
