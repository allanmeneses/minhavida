using System.Text;
using Circulos.Api.Auth;
using Circulos.Api.Contracts;
using Circulos.Api.Controllers;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Observability;
using Circulos.Api.Payments;
using Circulos.Api.Services;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<SessionService>();
builder.Services.Configure<SystemAdminOptions>(builder.Configuration.GetSection("SystemAdmin"));
builder.Services.PostConfigure<SystemAdminOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.Email))
    {
        options.Email = builder.Configuration["SYSTEM_ADMIN_EMAIL"];
    }
});
builder.Services.AddScoped<UsageStats>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<JobQueue>();
builder.Services.AddScoped<CircleArchiveService>();
builder.Services.AddScoped<OwnershipOfferService>();
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.Section));
builder.Services.PostConfigure<GoogleAuthOptions>(options =>
{
    if (string.Equals(builder.Configuration["GOOGLE_ENABLED"], "true", StringComparison.OrdinalIgnoreCase))
    {
        options.Enabled = true;
    }

    if (string.IsNullOrWhiteSpace(options.ClientId))
    {
        options.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"];
    }
});
builder.Services.AddHttpClient("google-auth", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<JobWorker>();
}
builder.Services.AddSingleton<IEncryptionKeyProvider, ConfigurationEncryptionKeyProvider>();
builder.Services.AddSingleton<LocalFileStorage>();
builder.Services.AddSingleton<IFileStorage>(sp =>
    new EncryptedFileStorage(
        sp.GetRequiredService<LocalFileStorage>(),
        sp.GetRequiredService<IEncryptionKeyProvider>()));
builder.Services.AddScoped<QuotaService>();
builder.Services.AddScoped<IEmailSender, OutboxEmailSender>();
builder.Services.AddScoped<ReminderEmailService>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<StatusPage>();
builder.Services.AddScoped<RecapService>();
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection("Billing"));
builder.Services.Configure<PlanPriceOptions>(builder.Configuration.GetSection("Plans"));
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection("MercadoPago"));
builder.Services.PostConfigure<MercadoPagoOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.AccessToken))
    {
        options.AccessToken = builder.Configuration["MP_ACCESS_TOKEN"];
    }

    if (string.IsNullOrWhiteSpace(options.WebhookSecret))
    {
        options.WebhookSecret = builder.Configuration["MP_WEBHOOK_SECRET"];
    }
});
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<SuccessionService>();
if (PaymentsConfig.IsMercadoPago(builder.Configuration))
{
    builder.Services.AddHttpClient<IPaymentGateway, MercadoPagoGateway>(client =>
        client.Timeout = TimeSpan.FromSeconds(20));
}
else
{
    builder.Services.AddSingleton<FakePaymentGateway>();
    builder.Services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<FakePaymentGateway>());
}

builder.Services.AddCirculosObservability(builder.Configuration);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = FileRules.MaxIncomingBytes + (1024 * 1024);
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=127.0.0.1;Port=5432;Database=circulos;Username=circulos;Password=circulos";
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
        return;
    }

    options.UseNpgsql(connectionString);
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key não configurada.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero,
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
    });

builder.Services.AddAuthorization();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ??
[
    "http://127.0.0.1:43123",
    "http://localhost:43123",
];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        db.Database.EnsureCreated();
    }
    else
    {
        db.Database.Migrate();
    }

    await TemplateCatalog.EnsureSeededAsync(db, CancellationToken.None);
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/api/account/appeals", StringComparison.OrdinalIgnoreCase))
        {
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            var userId = context.User.GetUserId();
            var blocked = await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.BlockedAt != null);
            if (blocked)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Esta conta foi bloqueada."));
                return;
            }
        }
    }

    await next();
});

app.MapGet("/", () => Results.Json(new { name = "Círculos", version = "2.0.0" }));
app.MapControllers();
if (PaymentsConfig.IsSimulated(app.Configuration))
{
    app.MapMethods("/dev/rotina", new[] { "GET", "POST" }, () => Results.Ok(new { ok = true, provider = PaymentsConfig.Simulated }));
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapPost("/dev/circles/{id:guid}/ownership-due", async (
        Guid id,
        AppDbContext db,
        OwnershipOfferService offers,
        TimeProvider clock,
        HttpContext http,
        CancellationToken cancellationToken) =>
    {
        if (http.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userId = http.User.GetUserId();
        var member = await db.CircleMembers.AnyAsync(
            item => item.CircleId == id && item.UserId == userId,
            cancellationToken);
        if (!member)
        {
            return Results.NotFound(new ErrorResponse("Círculo não encontrado."));
        }

        var circle = await db.Circles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (circle is null || circle.Status == CircleStatus.Deleted)
        {
            return Results.NotFound(new ErrorResponse("Círculo não encontrado."));
        }

        if (circle.OwnerUserId is not null)
        {
            return Results.Conflict(new ErrorResponse("Círculo ainda tem dono."));
        }

        var now = clock.GetUtcNow().UtcDateTime;
        circle.OwnerLostAt = now.AddDays(-OwnershipOfferService.WaitDays);
        circle.OwnershipNextCheckAt = now.AddMinutes(-1);
        var openVotes = await db.SuccessionVotes
            .Where(vote => vote.CircleId == id && vote.Status == SuccessionVoteStatus.Open)
            .ToListAsync(cancellationToken);
        foreach (var vote in openVotes)
        {
            vote.DeadlineAt = now.AddMinutes(-1);
        }

        await db.SaveChangesAsync(cancellationToken);
        await offers.ProcessDueAsync(cancellationToken);
        return Results.NoContent();
    }).RequireAuthorization();
}

app.Run();

public partial class Program;
