# What happens when a City is searched for the first time?

Type: grilling
Status: resolved
Blocked by: 01

## Question

A never-seen City costs one Gemini call plus ~20 OMDb calls (seconds of latency, API cost). Synchronous request, background job with polling/status, pre-seeding the curated dropdown cities, or a mix? What if two users search the same new City at once? What does a partial failure (some OMDb lookups fail) leave behind?

## Answer

Synchronous, with a per-City lock. Partial results are returned but not saved. Complete empty results are saved.

Details: openspec/changes/add-city-movie-search/design.md
