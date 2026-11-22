using System.Text;
using Circulos.Api.Auth;
using Circulos.Api.Data;
using Circulos.Api.Domain;
using Circulos.Api.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = FileRules.MaxDocumentBytes + (1024 * 1024);
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=127.0.0.1;Port=5432;Database=circulos;Username=circulos;Password=circulos";

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

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
            ClockSkew = TimeSpan.FromMinutes(1),
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
    db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Json(new { name = "Circulos", version = "1.0.0" }));
app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapControllers();

app.Run();

public partial class Program;
