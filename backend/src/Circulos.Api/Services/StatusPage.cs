using System.Globalization;
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
        var overall = snapshot.Database == "ok" ? "Operando" : "Instável";
        var checkedAt = snapshot.CheckedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        return "<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
            + "<title>Status do Círculos</title>"
            + "<style>body{font-family:sans-serif;margin:32px auto;max-width:720px;color:#1b1b1b;background:#f7f4ef;display:flex;flex-direction:column;align-items:center;}"
            + "h1{margin:0 0 8px;text-align:center;}p{color:#5c564d;text-align:center;max-width:560px;}"
            + "dl{background:#fff;padding:16px;border-radius:8px;width:100%;max-width:520px;}"
            + "dt{font-weight:700;margin-top:12px;}dd{margin:4px 0 0;}</style></head><body>"
            + "<h1>Status do Círculos</h1>"
            + "<p>Página automática a partir do monitoramento. Checagem pública do serviço.</p><dl>"
            + "<dt>Geral</dt><dd>" + overall + "</dd>"
            + "<dt>API</dt><dd>" + (snapshot.Api == "ok" ? "operando" : "fora") + "</dd>"
            + "<dt>Banco</dt><dd>" + (snapshot.Database == "ok" ? "operando" : "fora") + "</dd>"
            + "<dt>Telemetria</dt><dd>" + (snapshot.Telemetry == "on" ? "ligada" : "desligada") + "</dd>"
            + "<dt>Atualizado</dt><dd>" + checkedAt + "</dd>"
            + "</dl></body></html>";
    }
}

public sealed record StatusSnapshot(string Api, string Database, string Telemetry, DateTime CheckedAt);
