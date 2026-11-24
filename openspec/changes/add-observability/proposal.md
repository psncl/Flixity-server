# Proposal

## Why

Failed searches and poor LLM suggestions need to be visible. Nothing in the product reads failures back, so they belong in telemetry and not in a database table.

## What Changes

- Log every search with City ID, LLM provider and model, movies suggested and saved, duration and outcome.
- Record user-facing and upstream failures at defined severities.
- Trace incoming requests, outbound HTTP calls and database calls.
- Keep API keys and IP addresses out of telemetry.
- Alert on repeated 503 responses.

## Capabilities

### New Capabilities
- `search-telemetry`: what is recorded about searches and failures, and what must never be recorded.

### Modified Capabilities

None.

## Impact

- Depends on `add-city-movie-search`.
- Must be complete before `deploy-to-azure`.
- New dependencies: OpenTelemetry, Azure Monitor (production), Aspire dashboard (development).
