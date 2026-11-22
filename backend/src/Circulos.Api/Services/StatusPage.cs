using Circulos.Api.Data;
using Circulos.Api.Observability;
using Microsoft.EntityFrameworkCore;

namespace Circulos.Api.Services;

public sealed class StatusPage(AppDbContext db, IConfiguration configuration, TimeProvider clock)
{
    public async Task<StatusSnapshot> SnapshotAsync(CancellationToken cancellationToken)
    {
        var database = await db.Database.CanConnectAsync(cancellationToken) ? "ok" : "down";

        return new StatusSnapshot(
            "ok",
            database,
            ObservabilitySetup.IsEnabled(configuration) ? "on" : "off",
            clock.GetUtcNow().UtcDateTime);
    }

    public static string Html(StatusSnapshot snapshot)
    {
        var overall = snapshot.Database == "ok" ? "Operando" : "Instavel";
        var checkedAt = snapshot.CheckedAt.ToString("O");
        return "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<title>Status do Circulos</title>"
            + "<style>body{font-family:sans-serif;margin:32px;color:#1b1b1b;background:#f7f4ef;}"
            + "h1{margin:0 0 8px;}p{color:#5c564d;}"
            + "dl{background:#fff;padding:16px;border-radius:8px;max-width:420px;}"
            + "dt{font-weight:700;margin-top:12px;}dd{margin:4px 0 0;}</style></head><body>"
            + "<h1>Status do Circulos</h1>"
            + "<p>Pagina automatica a partir do monitoramento da API.</p><dl>"
            + "<dt>Geral</dt><dd>" + overall + "</dd>"
            + "<dt>API</dt><dd>" + snapshot.Api + "</dd>"
            + "<dt>Banco</dt><dd>" + snapshot.Database + "</dd>"
            + "<dt>OpenTelemetry</dt><dd>" + snapshot.Telemetry + "</dd>"
            + "<dt>Atualizado</dt><dd>" + checkedAt + "</dd>"
            + "</dl></body></html>";
    }
}

public sealed record StatusSnapshot(string Api, string Database, string Telemetry, DateTime CheckedAt);
