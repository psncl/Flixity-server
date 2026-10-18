# What counts as a failed search, and where is it recorded?

Type: grilling
Status: open
Blocked by: 02, 06, 08

## Question

Two categories: (1) user-facing failures (unresolvable City, zero Movies found, timeouts) and (2) upstream failures (Gemini/OMDb errors, rate limits, malformed LLM JSON, Movies Gemini suggested that OMDb can't find). Which are product data worth a queryable table vs operational telemetry/logs? What alerts, if any?
