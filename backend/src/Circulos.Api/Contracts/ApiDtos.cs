using System.ComponentModel.DataAnnotations;
using Circulos.Api.Domain;
using Circulos.Api.Reminders;

namespace Circulos.Api.Contracts;

public sealed record ErrorResponse(string Error);

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome.")]
    [MinLength(2, ErrorMessage = "O nome precisa ter pelo menos 2 caracteres.")]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class DeleteAccountRequest
{
    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class ConfirmTokenRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

public sealed record AuthUserDto(Guid Id, string Email, string Name);

public sealed record AuthResponse(string Token, AuthUserDto User);

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MinLength(2, ErrorMessage = "O nome precisa ter pelo menos 2 caracteres.")]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(256)]
    [EmailAddress(ErrorMessage = "Informe um e-mail de contato valido.")]
    public string? SecondaryEmail { get; set; }

    public bool IsPublic { get; set; }
}

public sealed record ProfileResponse(
    string DisplayName,
    DateOnly? BirthDate,
    string? Phone,
    string? SecondaryEmail,
    bool IsPublic,
    bool HasPhoto);

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);

public sealed record LifeAreaResponse(Guid Id, string Name, string Kind, int SortOrder, bool Suggested);

public sealed class CreateLifeAreaRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;
}

public sealed class LifeEntryRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20000)]
    public string? Body { get; set; }

    public decimal? NumberValue { get; set; }

    public DateOnly? OccurredOn { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }
}

public sealed record LifeEntryResponse(
    Guid Id,
    Guid LifeAreaId,
    string Title,
    string? Body,
    decimal? NumberValue,
    DateOnly? OccurredOn,
    string? Location,
    bool HasPhoto,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class CreateCircleRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public string Visibility { get; set; } = "private";
    public bool Discoverable { get; set; }
    public bool IsSurprise { get; set; }
    public string? SurpriseSubjectEmail { get; set; }
    public string Kind { get; set; } = "lasting";
    public string? SuccessorEmail { get; set; }
}

public sealed class InviteCircleRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = "viewer";
}

public sealed class TransferCircleRequest
{
    [Required]
    public Guid MemberUserId { get; set; }
}

public sealed class CirclePostRequest
{
    [MaxLength(8000)]
    public string Body { get; set; } = string.Empty;
}

public sealed record CircleMemberDto(Guid UserId, string Name, string Role);
public sealed record CircleSummaryResponse(
    Guid Id,
    string? Name,
    string? Description,
    string Visibility,
    bool Discoverable,
    bool IsSurprise,
    bool Revealed,
    string Kind,
    string Status,
    string Role,
    int MemberCount);

public sealed record CircleJoinRequestDto(Guid Id, Guid UserId, string Name, string Status, DateTime CreatedAt);

public sealed record CircleDetailResponse(
    Guid Id,
    string? Name,
    string? Description,
    string Visibility,
    bool Discoverable,
    bool IsSurprise,
    bool Revealed,
    string Kind,
    string Status,
    Guid? OwnerUserId,
    IReadOnlyList<CircleMemberDto> Members,
    IReadOnlyList<CirclePostResponse> Posts,
    IReadOnlyList<CircleLogResponse> Log,
    IReadOnlyList<CircleJoinRequestDto> JoinRequests);

public sealed record CirclePostResponse(Guid Id, Guid? AuthorUserId, string Body, bool HasPhoto, DateTime CreatedAt);
public sealed record CircleLogResponse(Guid Id, Guid? ActorUserId, string Action, string Detail, DateTime CreatedAt);
public sealed record InviteCreatedResponse(string Token);

public sealed class ReminderRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Kind { get; set; } = "general";
    public RecurrenceRule Recurrence { get; set; } = new();
    public DateTime StartAt { get; set; }
    public string? PersonName { get; set; }
    public int AdvanceDays { get; set; }
    public string[] GiftIdeas { get; set; } = [];
}

public sealed record ReminderResponse(
    Guid Id,
    string Title,
    string Kind,
    RecurrenceRule Recurrence,
    DateTime StartAt,
    DateTime NextFireAt,
    bool IsPaused,
    bool SkipNext,
    string? PersonName,
    int AdvanceDays,
    string[] GiftIdeas,
    bool EndingSoon);

public sealed class VacationRequest
{
    public DateTime? Until { get; set; }
}

public sealed class ShortcutRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(80)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Color { get; set; } = "#1F6F66";

    public int SortOrder { get; set; }
}

public sealed record ShortcutResponse(Guid Id, string Label, string Color, int SortOrder, bool IsActive);

public sealed record PlanResponse(
    string Plan,
    int? CircleLimit,
    int CircleCount,
    long StorageLimitBytes,
    long StorageUsedBytes,
    bool StorageWarning,
    string Checkout);

public sealed record DayPanelItem(Guid Id, string Title, string Kind, DateTime At, bool Overdue);

public sealed record DayPanelResponse(
    IReadOnlyList<DayPanelItem> Overdue,
    IReadOnlyList<DayPanelItem> DueToday,
    IReadOnlyList<DayPanelItem> Upcoming,
    int ActiveCircleCount,
    IReadOnlyList<ShortcutResponse> Shortcuts,
    DateTime? VacationUntil,
    PlanResponse Plan);

public static class EnumText
{
    public static string Plan(PlanKind plan) => plan == PlanKind.Paid ? "paid" : "free";
    public static string Area(LifeAreaKind kind) => kind switch
    {
        LifeAreaKind.Vaccine => "vaccine",
        LifeAreaKind.Reminder => "reminder",
        LifeAreaKind.Document => "document",
        LifeAreaKind.Diary => "diary",
        _ => "custom",
    };

    public static string Visibility(CircleVisibility value) => value == CircleVisibility.Public ? "public" : "private";
    public static CircleVisibility ParseVisibility(string? value) =>
        string.Equals(value, "public", StringComparison.OrdinalIgnoreCase) ? CircleVisibility.Public : CircleVisibility.Private;

    public static string Kind(CircleKind value) => value switch
    {
        CircleKind.Temporary => "temporary",
        CircleKind.Daily => "daily",
        _ => "lasting",
    };

    public static CircleKind ParseCircleKind(string? value) => value?.ToLowerInvariant() switch
    {
        "temporary" => CircleKind.Temporary,
        "daily" => CircleKind.Daily,
        _ => CircleKind.Lasting,
    };

    public static string Status(CircleStatus value) => value switch
    {
        CircleStatus.Archived => "archived",
        CircleStatus.LockedNoOwner => "locked",
        _ => "active",
    };

    public static string Role(CircleRole value) => value.ToString().ToLowerInvariant();

    public static CircleRole ParseRole(string? value) => value?.ToLowerInvariant() switch
    {
        "commenter" => CircleRole.Commenter,
        "editor" => CircleRole.Editor,
        "admin" => CircleRole.Admin,
        "owner" => CircleRole.Owner,
        _ => CircleRole.Viewer,
    };

    public static string ReminderKind(ReminderKind value) => value == Domain.ReminderKind.Birthday ? "birthday" : "general";
    public static ReminderKind ParseReminderKind(string? value) =>
        string.Equals(value, "birthday", StringComparison.OrdinalIgnoreCase) ? Domain.ReminderKind.Birthday : Domain.ReminderKind.General;
}
