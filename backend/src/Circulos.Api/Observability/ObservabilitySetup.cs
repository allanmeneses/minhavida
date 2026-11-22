using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Circulos.Api.Observability;

public static class ObservabilitySetup
{
    public static IServiceCollection AddCirculosObservability(this IServiceCollection services, IConfiguration configuration)
    {
        if (!IsEnabled(configuration))
        {
            return services;
        }

        var serviceName = configuration["OTEL_SERVICE_NAME"]
            ?? configuration["Observability:ServiceName"]
            ?? "circulos-api";
        var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["Observability:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddRuntimeInstrumentation();
                metrics.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }

    public static bool IsEnabled(IConfiguration configuration)
    {
        var raw = configuration["OTEL_ENABLED"] ?? configuration["Observability:Enabled"] ?? "false";
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase);
    }
}
