# What happens when a City is searched for the first time?

Type: grilling
Status: open
Blocked by: 01

## Question

A never-seen City costs one Gemini call plus ~20 OMDb calls (seconds of latency, API cost). Synchronous request, background job with polling/status, pre-seeding the curated dropdown cities, or a mix? What if two users search the same new City at once? What does a partial failure (some OMDb lookups fail) leave behind?
