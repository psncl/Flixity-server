# Design

## Context

Based on October 2026 research: `research/observability-tooling.md` on branch `origin/research/observability-tooling`. Findings that shape this design:

- Microsoft recommends `Microsoft.OpenTelemetry` for new apps over `Azure.Monitor.OpenTelemetry.AspNetCore`; it can export to Azure Monitor and OTLP together.
- 5 GB/month is free per billing account, with 90 days of retention. Default sampling is 5 traces/s.
- `ILogger` template placeholders become queryable fields. Npgsql is not traced by default.
- Azure custom events rely on an Azure-only attribute, so prefer ordinary logs and metrics.

## Goals / Non-Goals

**Goals:** diagnose failures and compare models using standard OpenTelemetry.

**Non-Goals:** a failures table, usage analytics.

## Decisions

- **`Microsoft.OpenTelemetry`:** OTLP to the Aspire dashboard in development, Azure Monitor in production. Instrumentation stays vendor-neutral.
- **HTTP and database:** outbound HTTP is traced automatically; add the Npgsql source and meter. EF Core uses Npgsql underneath.
- **LLM:** add `IChatClient` OpenTelemetry middleware for model and token data. Do not record prompt or response content.
- **Severities:**

| Event | Level |
|---|---|
| No matching City, empty result, rate limit hit | Information |
| OMDb cannot find a suggested movie (title and year) | Warning |
| OMDb error or quota, Wikidata failure | Warning |
| LLM or geocoder failure, invalid LLM data | Error |

- **Search event:** City ID, provider, model, suggested count, saved count, duration, outcome.
- **Privacy:** never record IP addresses or API keys. OMDb's key is in its query string, so test that logs and spans redact it.
- **Cost:** keep default sampling, with a daily cap of about 0.15 GB and an over-quota alert.
- **Alert:** repeated 503 responses.

## Risks / Trade-offs

- [Sampling hides some traces] → accepted at low traffic.
- [A library update changes redaction] → keep an automated redaction test.

## Open Questions

- Alert threshold and period.
