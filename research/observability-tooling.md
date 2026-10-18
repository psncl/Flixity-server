# Observability tooling for the Flixity backend

Question: which observability tooling fits a low-traffic .NET 10 minimal API on Azure App Service (free trial) that calls Gemini (Google.GenAI SDK), OMDb (typed HttpClient) and Postgres (Npgsql + Dapper)? This covers cost, ILogger, custom telemetry, automatic dependency tracking, and avoiding lock-in.

Researched 2026-10-18, mostly from Microsoft Learn, NuGet, and the SDK source on GitHub.

## 1. Microsoft's distro options (what changed recently)

There are now **two** Microsoft OpenTelemetry distros for .NET:

| Package | Status | Notes |
|---|---|---|
| `Microsoft.OpenTelemetry` (v1.2.0 stable, 2026-10-15) | **What Microsoft now recommends for new apps** | `builder.UseMicrosoftOpenTelemetry(o => o.Exporters = ExportTarget.AzureMonitor)`. The `ExportTarget` flags are `AzureMonitor`, `Otlp`, `Console` and `Agent365`, and they can be combined. Depends on both `Azure.Monitor.OpenTelemetry.Exporter` and `OpenTelemetry.Exporter.OpenTelemetryProtocol`. |
| `Azure.Monitor.OpenTelemetry.AspNetCore` (v1.6.0, 2026-07-27) | Supported, not deprecated | `builder.Services.AddOpenTelemetry().UseAzureMonitor()`. Exports to Azure Monitor only. |

- The Learn "Enable OpenTelemetry" page says: "New app or adopting OpenTelemetry for the first time: Use the Microsoft OpenTelemetry Distro… Already using the Azure Monitor OpenTelemetry Distro: No action needed." It also says "Don't initialize both distros in the same application." — https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore
- NuGet: https://www.nuget.org/packages/Microsoft.OpenTelemetry, https://www.nuget.org/packages/Azure.Monitor.OpenTelemetry.AspNetCore
- ExportTarget values, custom sources and meters, and instrumentation toggles: https://github.com/microsoft/opentelemetry-distro-dotnet/blob/main/docs/azure-monitor-getting-started.md
- Both distros bundle ASP.NET Core, HttpClient and SqlClient instrumentation, plus ILogger log export and standard metrics. `Microsoft.OpenTelemetry` also bundles Azure SDK, OpenAI and Semantic Kernel instrumentation. — https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-collect-detect#included-instrumentation-libraries
- Both distros are layers over the standard OpenTelemetry .NET SDK. Your code uses plain `ILogger`, `ActivitySource` and `Meter`, so the distro choice only affects startup wiring.
- App Service also has codeless (agent-based) auto-instrumentation. Microsoft says to choose one path and "avoid overlapping instrumentation for the same app". For code-based setup, use OpenTelemetry. — https://learn.microsoft.com/azure/azure-monitor/app/application-insights-faq

## 2. Cost: free allowance and controls

- **Free allowance:** the default pay-as-you-go Log Analytics tier "includes 5 GB per month of free data allowance per billing account". Application Insights is billed through the Log Analytics workspace that holds its data. — https://learn.microsoft.com/azure/azure-monitor/app/application-insights-faq#what-is-the-application-insights-pricing-model
- **Retention:** Analytics Logs keep data free for 31 days, and Application Insights data (classic or workspace-based) for 90 days. Retention beyond that is billed per GB per month. Live Metrics has no data-volume charge. — https://azure.microsoft.com/pricing/details/monitor/, https://learn.microsoft.com/azure/azure-monitor/logs/cost-logs#application-insights-billing
- The per-GB price varies by region. Check the pricing calculator.
- **Sampling (in the SDK):** both distros default to **rate-limited sampling at 5 traces/second**. You can change it with `TracesPerSecond`, or switch to percentage sampling with `SamplingRatio` and `TracesPerSecond = null`. The env vars `OTEL_TRACES_SAMPLER=microsoft.rate_limited|microsoft.fixed_percentage` and `OTEL_TRACES_SAMPLER_ARG` also work. The .NET distro enables trace-based log sampling by default: logs tied to an unsampled trace are dropped. Metrics are pre-aggregated, so sampling doesn't affect them. — https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-sampling, https://learn.microsoft.com/dotnet/api/overview/azure/monitor.opentelemetry.aspnetcore-readme
- **Daily cap:** you can set one on the Log Analytics workspace (Usage and estimated costs > Daily Cap). For workspace-based Application Insights, "the effective daily cap is the minimum of the two settings". Microsoft treats the cap as a safety net, not a cost-cutting tool: once it's hit, collection stops until the workspace's fixed reset hour, and a little data above the cap still gets billed. Microsoft recommends adding an alert on `_LogOperation | where Category =~ "Ingestion" | where Detail contains "OverQuota"`. — https://learn.microsoft.com/azure/azure-monitor/logs/daily-cap
- Rough size: about 160 MB/day keeps you under 5 GB/month. Events are typically 2-10 KB. — https://learn.microsoft.com/dynamics365/business-central/dev-itpro/administration/telemetry-control-cost (Microsoft doc, but written for Business Central, so treat it as a rule of thumb)
- Other levers: filter requests such as health checks with `AspNetCoreTraceInstrumentationOptions.Filter`, drop metric instruments with views, and turn off Live Metrics (`EnableLiveMetrics = false`). — distro README above

**For Flixity:** at learning-project traffic, the 5 GB/month allowance is far more than needed. Set a daily cap of about 0.15 GB with an alert, and keep the default 5 traces/s sampler.

## 3. Structured logging with ILogger

- Both distros pick up `ILogger` automatically. Records land in the `traces` table (`AppTraces` in the workspace). Placeholders in the message template become `customDimensions`, so `logger.LogInformation("Found {MovieCount} movies for {City}", n, city)` gives you queryable `MovieCount` and `City` fields. Log levels are still controlled by `Logging:LogLevel` in appsettings. — https://learn.microsoft.com/azure/azure-monitor/app/application-insights-faq (ILogger customization), https://learn.microsoft.com/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme
- Log scopes are **off by default** in OpenTelemetry. Turn them on with `OpenTelemetryLoggerOptions.IncludeScopes = true`. — exporter README above
- Logs written during a request are correlated with that request's trace automatically, through `Activity.Current`.

## 4. Custom events and metrics

- **Custom metrics:** use `System.Diagnostics.Metrics.Meter` (a counter or histogram) and register it with `ConfigureOpenTelemetryMeterProvider(b => b.AddMeter("Flixity"))`. This is standard OpenTelemetry, so it works with any backend. — https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-add-modify#add-custom-metrics
- **Custom spans:** create an `ActivitySource("Flixity")` and register it with `AddSource("Flixity")`. Client and internal spans show up as dependencies, and server spans as requests. — same page
- **Custom events (the `customEvents` table):** there's no `TrackEvent` in the OpenTelemetry distros. Instead, use `ILogger` with an attribute named `microsoft.custom_event.name` in the message template, for example `logger.LogInformation("{microsoft.custom_event.name} {City}", "CitySearched", city)`. This is **Azure-specific**: other backends just see an ordinary log line. A plain structured log or a counter is the portable option. — https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-add-modify#send-custom-events

## 5. Are outbound calls tracked as dependencies automatically?

| Dependency | Automatic? | Evidence / what to do |
|---|---|---|
| **OMDb** (typed `HttpClient`) | **Yes** | The distros bundle `OpenTelemetry.Instrumentation.Http`. On .NET 9+, `HttpClient` also emits complete spans natively from the `System.Net.Http` ActivitySource. Either way, every OMDb call becomes an HTTP dependency with method, URL, status and duration. — https://learn.microsoft.com/dotnet/fundamentals/networking/telemetry/tracing#instrumentation, https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing-builtin-activities |
| **Gemini** (`Google.GenAI` 1.26.x) | **Yes, but only as a generic HTTP call** | The SDK source (`Google.GenAI/ApiClient.cs`) sends requests through a plain `System.Net.Http.HttpClient`, created with `new HttpClient` or taken from `ClientOptions.HttpClientFactory`. The SDK has **no ActivitySource or Meter of its own** (a grep for `ActivitySource` and `System.Diagnostics.Metrics` in the SDK finds nothing). So each Gemini call shows up as `POST generativelanguage.googleapis.com/...` with duration and status, but **without model name or token counts**. The API key goes in the `x-goog-api-key` header, not the URL, so it doesn't leak into recorded URLs. — https://github.com/googleapis/dotnet-genai (`Google.GenAI/ApiClient.cs`, `HttpApiClient.cs`) |
| Gemini with GenAI semantics (optional) | Manual | Two options. (a) The SDK ships an `IChatClient` adapter (`client.AsIChatClient(model)`, backed by `GoogleGenAIChatClient`). Wrapping it with Microsoft.Extensions.AI `.UseOpenTelemetry()` emits `gen_ai.*` spans and the `gen_ai.client.token.usage` and `gen_ai.client.operation.duration` metrics. Prompts and responses are excluded unless `EnableSensitiveData` is set. (b) Wrap the call in your own `Activity` and add tags such as the model and `UsageMetadata` token counts. — https://learn.microsoft.com/dotnet/ai/ichatclient#use-telemetry, https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient |
| **Postgres** (Npgsql + Dapper) | **No, one line needed** | The distros' bundled SQL instrumentation covers only `Microsoft.Data.SqlClient` and `System.Data.SqlClient`, not Npgsql. Npgsql has a built-in ActivitySource named `"Npgsql"`. `Npgsql.OpenTelemetry` (v10.0.3) adds `AddNpgsql()`, which is literally `builder.AddSource("Npgsql")`. Dapper sits on top of Npgsql, so its queries get traced too. For metrics, Npgsql 8+ exposes a Meter named `"Npgsql"`, which you register with `AddMeter("Npgsql")`. Npgsql notes that the database semantic conventions are still experimental. — https://www.npgsql.org/doc/diagnostics/tracing.html, https://www.npgsql.org/doc/diagnostics/metrics.html, https://github.com/npgsql/npgsql/blob/main/src/Npgsql.OpenTelemetry/TracerProviderBuilderExtensions.cs, https://www.nuget.org/packages/Npgsql.OpenTelemetry |

Side note (not observability): `MovieInfoGemini` currently does `new Client(apiKey: _apiKey)` on every call. Each new `Client` creates its own `HttpClient`, which is the classic socket-exhaustion anti-pattern. A singleton `Client` (or `ClientOptions.HttpClientFactory` backed by `IHttpClientFactory`) would fix that. Tracing works either way.

## 6. Avoiding vendor lock-in

- **The instrumentation is already vendor-neutral.** `ILogger`, `ActivitySource`, `Meter` and the library instrumentations are all standard .NET and OpenTelemetry. Only the exporter and startup wiring are Azure-specific, and the one Azure-only API is `microsoft.custom_event.name`.
- **Plain OpenTelemetry route:** use `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` and `.UseOtlpExporter()`. Configure everything through the standard `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_SERVICE_NAME` env vars. On .NET 10, `OpenTelemetry.Instrumentation.Http` is optional: `AddSource("System.Net.Http")` is enough. — https://learn.microsoft.com/dotnet/core/diagnostics/observability-otlp-example
- **`Microsoft.OpenTelemetry` can export to Azure Monitor and OTLP at the same time** (`ExportTarget.AzureMonitor | ExportTarget.Otlp`). That makes moving to Grafana, Honeycomb and similar backends a configuration change. — distro docs above
- **Native OTLP ingestion into Azure Monitor** (sending OTLP straight to Application Insights) exists. The Collector path is GA, but the agent and AKS paths are preview, and it needs an OpenTelemetry Collector or the Azure Monitor Agent. That's too much infrastructure for one App Service app. — https://learn.microsoft.com/azure/azure-monitor/containers/opentelemetry-options
- **Local dev with the Aspire dashboard:** run `docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest`, then set `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`. You get traces, structured logs and metrics with no dependency on the rest of Aspire. Microsoft describes it as "a developer visualization tool, not a production monitoring tool". The container prints a login token on startup. — https://learn.microsoft.com/dotnet/core/diagnostics/observability-otlp-example. You could also scaffold `dotnet new aspire-servicedefaults` and call `builder.ConfigureOpenTelemetry()` without the Aspire AppHost. — https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel#opentelemetry-in-aspire

## Recommendation

1. Use **`Microsoft.OpenTelemetry`**, which Microsoft now recommends for new apps, through `builder.UseMicrosoftOpenTelemetry(...)`:
   - In development, export to **OTLP** pointed at the standalone **Aspire dashboard** container.
   - In Azure, export to **Azure Monitor**. Put `APPLICATIONINSIGHTS_CONNECTION_STRING` in App Service settings.
   - Choose the export target from config or environment, so there's one code path.
   - If you'd rather keep packages strictly upstream, the equivalent is plain OpenTelemetry + OTLP exporter for dev plus `Azure.Monitor.OpenTelemetry.Exporter` for prod.
2. Register `AddSource("Npgsql")` (or `AddNpgsql()`) and `AddMeter("Npgsql")`, plus your own `"Flixity"` ActivitySource and Meter. OMDb and Gemini are captured as HTTP dependencies with nothing extra.
3. Instrument Gemini yourself with a small custom span or counter that records model and token usage. For full `gen_ai.*` telemetry, go through `IChatClient` + `.UseOpenTelemetry()`.
4. Use structured `ILogger` templates. Prefer counters and structured logs over `microsoft.custom_event.name` events, so the telemetry stays portable.
5. Cost guardrails: keep the default 5 traces/s sampler, set a workspace daily cap of about 0.15 GB/day with an OverQuota alert, and filter health-check requests. At this traffic you should stay well inside the free 5 GB/month.
