using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AiOrchestrator.ServiceDefaults;

public static class ServiceDefaults
{
    public const string HealthEndpoint = "/api/health";
    public const string AliveEndpoint = "/api/alive";
    public const string VersionEndpoint = "/api/version";

    /// <summary>
    /// OpenTelemetry (logs, metrics, traces), service discovery, and resilient HTTP defaults for
    /// every service. Exporters are environment-driven: OTLP locally, Azure Monitor in the cloud
    /// when APPLICATIONINSIGHTS_CONNECTION_STRING is present (DEC-023).
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        builder
            .Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    static void ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder
            .Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
            )
            .WithTracing(tracing =>
                tracing
                    .AddSource(Diagnostics.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options =>
                        // Health probes fire constantly and say nothing about behaviour.
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpoint)
                            && !context.Request.Path.StartsWithSegments(AliveEndpoint)
                    )
                    .AddHttpClientInstrumentation()
            );

        // OTLP is the only exporter, and the Aspire dashboard is the only thing listening. A
        // cloud exporter would be one more package for a product that runs on your laptop.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpoint);
        app.MapHealthChecks(
            AliveEndpoint,
            new() { Predicate = registration => registration.Tags.Contains("live") }
        );
        return app;
    }

    /// <summary>
    /// The product's version, read from the entry assembly's <c>AssemblyInformationalVersion</c>,
    /// which MSBuild populates from <c>&lt;Version&gt;</c> in <c>src/Directory.Build.props</c>.
    /// One source, one read. The version is public information — the same as the health endpoint —
    /// so no permission is declared.
    /// </summary>
    public static WebApplication MapVersionEndpoint(this WebApplication app)
    {
        app.MapGet(
            VersionEndpoint,
            () =>
            {
                var version = Assembly
                    .GetEntryAssembly()
                    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion;
                return Results.Ok(new { version });
            }
        );
        return app;
    }
}
