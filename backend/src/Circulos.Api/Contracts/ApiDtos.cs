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

public sealed record AuthUserDto(Guid Id, string Email, string Name, bool IsSystemAdmin = false);

public sealed record AuthResponse(
    string? Token,
    string? RefreshToken,
    AuthUserDto? User,
    bool RequiresTwoFactor,
    string? TwoFactorToken);

public sealed class RefreshRequest
{
    public string? RefreshToken { get; set; }
}

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
}

public sealed class TwoFactorCodeRequest
{
    [MaxLength(16)]
    public string? Code { get; set; }

    [MaxLength(16)]
    public string? RecoveryCode { get; set; }
}

public sealed class TwoFactorVerifyRequest
{
    [Required]
    public string TwoFactorToken { get; set; } = string.Empty;

    [MaxLength(16)]
    public string? Code { get; set; }

    [MaxLength(16)]
    public string? RecoveryCode { get; set; }
}

public sealed class TwoFactorDisableRequest
{
    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(16)]
    public string? Code { get; set; }

    [MaxLength(16)]
    public string? RecoveryCode { get; set; }
}

public sealed record TwoFactorStatusResponse(bool Enabled);

public sealed record TwoFactorSetupResponse(
    string Secret,
    string OtpauthUri,
    string QrPngBase64,
    IReadOnlyList<string> RecoveryCodes);

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

    [MaxLength(500)]
    public string? Bio { get; set; }

    public bool FindableByLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public sealed record ProfileResponse(
    string DisplayName,
    DateOnly? BirthDate,
    string? Phone,
    string? SecondaryEmail,
    bool IsPublic,
    bool HasPhoto,
    string? Bio,
    bool FindableByLocation);

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

    public string[]? Tags { get; set; }

    public Guid[]? CircleIds { get; set; }
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
    DateTime UpdatedAt,
    IReadOnlyList<string> Tags,
    IReadOnlyList<Guid> CircleIds);

public sealed class ShareEntryRequest
{
    public Guid[] CircleIds { get; set; } = [];
}

public sealed record SharedLifeEntryResponse(
    Guid Id,
    Guid LifeAreaId,
    string Title,
    string? Body,
    decimal? NumberValue,
    DateOnly? OccurredOn,
    string? Location,
    bool HasPhoto,
    IReadOnlyList<string> Tags,
    Guid AuthorUserId,
    string AuthorName,
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
    public bool FindableByLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsSurprise { get; set; }
    public string? SurpriseSubjectEmail { get; set; }
    public string Kind { get; set; } = "lasting";
    public string? SuccessorEmail { get; set; }
    public DateTime? EndsAt { get; set; }
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
    bool FindableByLocation,
    bool IsSurprise,
    bool Revealed,
    string Kind,
    string Status,
    string Role,
    int MemberCount,
    DateTime? EndsAt = null);

public sealed record CircleJoinRequestDto(Guid Id, Guid UserId, string Name, string Status, DateTime CreatedAt);

public sealed record CircleDetailResponse(
    Guid Id,
    string? Name,
    string? Description,
    string Visibility,
    bool Discoverable,
    bool FindableByLocation,
    bool IsSurprise,
    bool Revealed,
    string Kind,
    string Status,
    Guid? OwnerUserId,
    IReadOnlyList<CircleMemberDto> Members,
    IReadOnlyList<CirclePostResponse> Posts,
    IReadOnlyList<CircleLogResponse> Log,
    IReadOnlyList<CircleJoinRequestDto> JoinRequests,
    IReadOnlyList<SharedLifeEntryResponse> SharedEntries,
    DateTime? EndsAt = null);

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
    public Guid? CircleId { get; set; }
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
    Guid? CircleId,
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
    public Guid? ReminderId { get; set; }

    [MaxLength(20)]
    public string? ActionKind { get; set; }
}

public sealed class ReorderShortcutsRequest
{
    public Guid[] Ids { get; set; } = [];
}

public sealed class ShareShortcutRequest
{
    [Required]
    public Guid CircleId { get; set; }
}

public sealed record ShortcutNoticeResponse(Guid Id, Guid ShortcutId, string Detail, DateTime CreatedAt);

public sealed record ShortcutResponse(
    Guid Id,
    string Label,
    string Color,
    int SortOrder,
    bool IsActive,
    Guid? ReminderId,
    string ActionKind,
    bool IsDeleted,
    Guid? SharedByUserId = null,
    string? SharedByName = null,
    Guid? CircleId = null);

public sealed class EmailPreferenceRequest
{
    public bool Enabled { get; set; }
}

public sealed record EmailPreferenceResponse(bool Enabled);

public sealed record SearchHit(string Type, Guid Id, string Title, string? Detail);
public sealed record SearchHistoryResponse(IReadOnlyList<string> Recent, IReadOnlyList<string> Frequent);
public sealed record SearchResponse(
    IReadOnlyList<SearchHit> Hits,
    SearchHistoryResponse History);

public sealed class SharedListRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Kind { get; set; } = "other";

    public Guid? CircleId { get; set; }

    [MaxLength(20)]
    public string MemberAccess { get; set; } = "view";
}

public sealed class SharedListAccessRequest
{
    [Required]
    [MaxLength(20)]
    public string MemberAccess { get; set; } = "view";
}

public sealed class SharedListGrantRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Access { get; set; } = "view";
}

public sealed class SharedListItemRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Text { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Quantity { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime? KnownUpdatedAt { get; set; }
}

public sealed class ReorderListItemsRequest
{
    public Guid[] Ids { get; set; } = [];
}

public sealed record SharedListSummaryResponse(
    Guid Id,
    string Name,
    string Kind,
    Guid? CircleId,
    string MemberAccess,
    int ItemCount,
    int DoneCount);

public sealed record SharedListMemberAccessResponse(Guid UserId, string Name, string Access, bool Overridden);

public sealed record SharedListItemResponse(
    Guid Id,
    string Text,
    string? Quantity,
    string? Note,
    bool IsDone,
    int SortOrder,
    Guid? AddedByUserId,
    string? AddedByName,
    Guid? DoneByUserId,
    string? DoneByName,
    DateTime UpdatedAt,
    bool LastSyncWins);

public sealed record SharedListDetailResponse(
    Guid Id,
    string Name,
    string Kind,
    Guid? CircleId,
    string MemberAccess,
    string MyAccess,
    bool CanAdd,
    bool CanComplete,
    bool CanManageItems,
    bool CanChangeAccess,
    IReadOnlyList<SharedListMemberAccessResponse> Members,
    IReadOnlyList<SharedListItemResponse> Items);

public sealed record PlanResponse(
    string Plan,
    int? CircleLimit,
    int CircleCount,
    long StorageLimitBytes,
    long StorageUsedBytes,
    bool StorageWarning,
    string Checkout,
    string SubscriptionStatus,
    bool InGoodStanding,
    int ShortcutLimit,
    bool Sandbox,
    string Provider,
    string? SubscriptionProviderId);

public sealed record PlanOfferResponse(
    string Id,
    string Name,
    decimal PriceReais,
    int? CircleLimit,
    long StorageLimitBytes,
    int ShortcutLimit,
    string Summary);

public sealed record PlanCatalogResponse(
    string Status,
    PlanOfferResponse Free,
    PlanOfferResponse PaidMonthly,
    PlanOfferResponse PaidYearly,
    bool Sandbox,
    string Provider);

public sealed class CheckoutRequest
{
    [MaxLength(20)]
    public string Period { get; set; } = "monthly";

    [MaxLength(80)]
    public string? CardToken { get; set; }
}

public sealed class SuccessionVoteRequest
{
    [Required]
    public Guid CandidateUserId { get; set; }
}

public sealed record SuccessionTallyResponse(Guid CandidateUserId, int Votes);

public sealed record SuccessionVoteResponse(
    Guid Id,
    string Status,
    DateTime OpenedAt,
    DateTime DeadlineAt,
    Guid? WinnerUserId,
    int MemberCount,
    IReadOnlyList<SuccessionTallyResponse> Tallies,
    Guid? MyVote);

public sealed class DeleteCircleRequest
{
    [Required]
    [MaxLength(40)]
    public string Confirmation { get; set; } = string.Empty;
}

public sealed class CircleLocationRequest
{
    public bool FindableByLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public sealed class NearbySearchRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusKm { get; set; } = 10;
}

public sealed record NearbyHitResponse(
    string Kind,
    Guid Id,
    string Name,
    string? Description,
    double DistanceKm);

public sealed class LivingProfileGrantRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Access { get; set; } = "view";
}

public sealed record LivingProfileGrantResponse(Guid UserId, string Name, string Access);

public sealed record DayPanelItem(Guid Id, string Title, string Kind, DateTime At, bool Overdue);

public sealed record DayPanelResponse(
    IReadOnlyList<DayPanelItem> Overdue,
    IReadOnlyList<DayPanelItem> DueToday,
    IReadOnlyList<DayPanelItem> Upcoming,
    int ActiveCircleCount,
    IReadOnlyList<ShortcutResponse> Shortcuts,
    DateTime? VacationUntil,
    PlanResponse Plan,
    IReadOnlyList<ShortcutNoticeResponse> ShortcutNotices,
    IReadOnlyList<string> ReminderEmailsSent,
    bool EmailRemindersEnabled);

public static class EnumText
{
    public static string Plan(PlanKind plan) => plan == PlanKind.Paid ? "paid" : "free";
    public static string Subscription(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.Active => "active",
        SubscriptionStatus.PastDue => "past_due",
        SubscriptionStatus.Suspended => "suspended",
        SubscriptionStatus.Cancelled => "cancelled",
        _ => "none",
    };
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
        CircleStatus.Deleted => "deleted",
        CircleStatus.Blocked => "blocked",
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

    public static string ListKind(SharedListKind value) => value switch
    {
        SharedListKind.Shopping => "shopping",
        SharedListKind.Wishes => "wishes",
        SharedListKind.Materials => "materials",
        _ => "other",
    };

    public static SharedListKind ParseListKind(string? value) => value?.ToLowerInvariant() switch
    {
        "shopping" or "compras" => SharedListKind.Shopping,
        "wishes" or "desejos" => SharedListKind.Wishes,
        "materials" or "materiais" => SharedListKind.Materials,
        _ => SharedListKind.Other,
    };

    public static string ListAccess(SharedListAccess value) => value switch
    {
        SharedListAccess.Add => "add",
        SharedListAccess.Complete => "complete",
        SharedListAccess.Manage => "manage",
        _ => "view",
    };

    public static SharedListAccess ParseListAccess(string? value) => value?.ToLowerInvariant() switch
    {
        "add" => SharedListAccess.Add,
        "complete" => SharedListAccess.Complete,
        "manage" => SharedListAccess.Manage,
        _ => SharedListAccess.View,
    };

    public static string LivingKind(LivingProfileKind value) => value switch
    {
        LivingProfileKind.Pet => "pet",
        LivingProfileKind.Group => "group",
        _ => "person",
    };

    public static LivingProfileKind ParseLivingKind(string? value) => value?.ToLowerInvariant() switch
    {
        "pet" => LivingProfileKind.Pet,
        "group" or "grupo" or "comunidade" => LivingProfileKind.Group,
        _ => LivingProfileKind.Person,
    };

    public static string LivingAccess(LivingProfileAccess value) =>
        value == LivingProfileAccess.Edit ? "edit" : "view";

    public static LivingProfileAccess ParseLivingAccess(string? value) =>
        string.Equals(value, "edit", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "editar", StringComparison.OrdinalIgnoreCase)
            ? LivingProfileAccess.Edit
            : LivingProfileAccess.View;

    public static string TemplateKind(RoutineTemplateKind value) =>
        value == RoutineTemplateKind.Reminder ? "reminder" : "list";

    public static RoutineTemplateKind ParseTemplateKind(string? value) =>
        string.Equals(value, "reminder", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "lembrete", StringComparison.OrdinalIgnoreCase)
            ? RoutineTemplateKind.Reminder
            : RoutineTemplateKind.List;
}

public sealed class MemoryRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(8000)]
    public string? Body { get; set; }

    public DateOnly? OccurredOn { get; set; }
    public Guid? CircleId { get; set; }
}

public sealed record MemoryResponse(
    Guid Id,
    Guid AuthorUserId,
    Guid? CircleId,
    Guid? SourceCircleId,
    string Title,
    string Body,
    DateOnly? OccurredOn,
    bool HasPhoto,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record RevealMessageDto(Guid PostId, Guid? AuthorUserId, string Body, bool HasPhoto, DateTime CreatedAt);

public sealed record RevealSummaryResponse(
    Guid CircleId,
    string Name,
    string Description,
    DateTime? RevealedAt,
    IReadOnlyList<RevealMessageDto> Messages,
    int PhotoCount);

public sealed record TemplateResponse(
    Guid Id,
    string Kind,
    string Name,
    string Description,
    int UseCount,
    int Rank,
    bool Curated);

public sealed class CreateTemplateRequest
{
    [Required]
    [MaxLength(20)]
    public string Kind { get; set; } = "list";

    [Required]
    [MinLength(2)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? ListId { get; set; }
    public Guid? ReminderId { get; set; }
}

public sealed record CalendarEventResponse(
    Guid ReminderId,
    string Title,
    string Kind,
    DateTime At,
    bool Paused,
    Guid? CircleId);

public sealed record CalendarMonthResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<CalendarEventResponse> Events,
    string? FeedUrl);

public sealed class LivingProfileRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Kind { get; set; } = "person";

    [MaxLength(200)]
    public string? Context { get; set; }

    public DateOnly? BirthDate { get; set; }
    public Guid? CircleId { get; set; }
}

public sealed record LivingProfileResponse(
    Guid Id,
    string Kind,
    string Name,
    string? Context,
    DateOnly? BirthDate,
    Guid? CircleId,
    bool HasPhoto,
    bool CanEdit,
    IReadOnlyList<LivingProfileGrantResponse>? Grants);

public sealed record StatusResponse(string Api, string Database, string Telemetry, DateTime CheckedAt);

public sealed record RecapFlowDto(string Key, string Label, int Count);

public sealed record ProfileRecapResponse(IReadOnlyList<RecapFlowDto> Flows);

public sealed record CircleRecapResponse(
    Guid CircleId,
    string Name,
    string Kind,
    string Status,
    string DisplayName,
    IReadOnlyList<RecapFlowDto> Flows,
    IReadOnlyList<string> MyTitles);

public sealed record RecapShareCreatedResponse(string Token, string Path);

public sealed record PublicProfileResponse(Guid UserId, string DisplayName, bool HasPhoto, string? Description);

public sealed class ReportRequest
{
    [Required]
    [MaxLength(20)]
    public string TargetKind { get; set; } = string.Empty;

    [Required]
    public Guid TargetId { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public sealed record ReportCreatedResponse(Guid Id);

public sealed record ReportItemResponse(
    Guid Id,
    Guid ReporterUserId,
    string ReporterEmail,
    string TargetKind,
    Guid TargetId,
    string? Note,
    DateTime CreatedAt);

public sealed class AdminBlockRequest
{
    [Required]
    [MaxLength(20)]
    public string TargetKind { get; set; } = string.Empty;

    [Required]
    public Guid TargetId { get; set; }

    public bool Blocked { get; set; } = true;
}

public sealed record AdminStatsResponse(long ShortcutsTriggered, long EntriesCreated, long ActiveCircles);

public sealed record AdminActionResponse(
    Guid Id,
    Guid AdminUserId,
    string Action,
    string TargetKind,
    Guid TargetId,
    string Detail,
    DateTime CreatedAt);

public sealed record AdminBlockListResponse(
    IReadOnlyList<AdminBlockedUserResponse> Users,
    IReadOnlyList<AdminBlockedCircleResponse> Circles);

public sealed record AdminBlockedUserResponse(Guid Id, string Email, DateTime BlockedAt);

public sealed record AdminBlockedCircleResponse(Guid Id, string Name, DateTime? BlockedAt);
