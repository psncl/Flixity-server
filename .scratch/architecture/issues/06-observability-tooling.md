# What observability tooling fits a .NET 10 API on Azure App Service?

Type: research
Status: resolved
Blocked by: 

## Question

Survey Application Insights / Azure Monitor OpenTelemetry distro and alternatives: free ingestion allowance, structured logging with ILogger, custom events/metrics, and whether outbound calls to Gemini (Google.GenAI SDK) and OMDb (HttpClient) are tracked as dependencies automatically or need manual instrumentation. Also how to avoid vendor lock-in (plain OpenTelemetry exporters).

## Answer

- Use the OpenTelemetry-based Microsoft distro. For new apps Microsoft now recommends `Microsoft.OpenTelemetry` (`builder.UseMicrosoftOpenTelemetry`, which can export to Azure Monitor and OTLP at the same time) over `Azure.Monitor.OpenTelemetry.AspNetCore`. The older package is still supported.
- Cost: 5 GB/month free per billing account, and Application Insights data is kept 90 days at no charge. The distro samples at 5 traces/s by default. Add a workspace daily cap of about 0.15 GB/day with an OverQuota alert as a safety net.
- `ILogger` is picked up automatically, and message-template placeholders become queryable `customDimensions`. Custom metrics and spans use standard `Meter` and `ActivitySource`. Custom events use `ILogger` with the `microsoft.custom_event.name` attribute, which works only in Azure.
- OMDb and Gemini calls are tracked automatically as HTTP dependencies. Google.GenAI uses a plain `HttpClient` and has no ActivitySource of its own, so you get duration and status but no model name or token counts. For those, add a custom span or use `AsIChatClient()` + M.E.AI `.UseOpenTelemetry()`. Npgsql (and so Dapper) is not traced by default: add `AddSource("Npgsql")` / `AddNpgsql()` and `AddMeter("Npgsql")`.
- To avoid lock-in, the instrumentation stays standard .NET and OpenTelemetry. Export OTLP to the standalone Aspire dashboard container in dev and to Azure Monitor in prod. Switching backends is then a configuration change.

Findings: branch `research/observability-tooling`, file `research/observability-tooling.md`
