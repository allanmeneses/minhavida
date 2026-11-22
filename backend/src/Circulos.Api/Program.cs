using System.Text;
using Circulos.Api.Auth;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Observability;
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
builder.Services.AddSingleton<IEncryptionKeyProvider, ConfigurationEncryptionKeyProvider>();
builder.Services.AddSingleton<LocalFileStorage>();
builder.Services.AddSingleton<IFileStorage>(sp =>
    new EncryptedFileStorage(
        sp.GetRequiredService<LocalFileStorage>(),
        sp.GetRequiredService<IEncryptionKeyProvider>()));
builder.Services.AddScoped<QuotaService>();
builder.Services.AddScoped<IEmailSender, OutboxEmailSender>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<StatusPage>();
builder.Services.AddScoped<RecapService>();
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
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key nao configurada.");

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

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
    });
}
else
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod());
    });
}

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

app.MapGet("/", () => Results.Json(new { name = "Circulos", version = "2.0.0" }));
app.MapControllers();

app.Run();

public partial class Program;
