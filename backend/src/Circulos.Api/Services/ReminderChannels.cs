namespace Circulos.Api.Services;

public static class ReminderChannels
{
    public const string App = "app";
    public const string Email = "email";

    public static readonly string[] Allowed = [App, Email];

    public static bool SupportsSms => false;
}
