namespace Circulos.Api.Domain;

public enum SubscriptionStatus
{
    None = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Cancelled = 4,
}

public enum BillingPeriod
{
    Monthly = 0,
    Yearly = 1,
}
