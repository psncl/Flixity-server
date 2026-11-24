# Proposal

## Why

Users need the Movies filmed in a chosen City, with a Blurb for each. Results should be generated once and reused, because generation is slow and costs money.

## What Changes

- Add `POST /cities/{geoNamesId}/search`, returning up to 10 Movies with Blurbs.
- Generate a City's results synchronously on first search; serve later searches from the database only.
- Generate titles and Blurbs in one LLM call, without web search. Blurbs are labelled as AI-generated in the UI.
- Make the LLM provider and model switchable; start with OpenAI `gpt-6-luna` instead of Gemini.
- Define behaviour for LLM, OMDb and geocoder failures, partial results and empty results.
- Rate limit searches per IP.
- **BREAKING**: remove `GET /movies` and `POST /movies/search`.

## Capabilities

### New Capabilities
- `city-movie-search`: generating, caching and returning the Movies filmed in a City.

### Modified Capabilities

None.

## Impact

- Depends on `add-city-lookup`.
- Replaces `Google.GenAI` with `Microsoft.Extensions.AI` and its OpenAI adapter; needs an OpenAI key and an account spending limit.
- Adds Movie and FilmingLocation tables.
- Removes the `GetMovies` slice and its tests.
