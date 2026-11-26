# Design

## Context

`MovieInfoOmdbApiClient.FindMovieAsync` looks a suggestion up by title and year (`t=…&y=…`), and if that result does not pass `MovieMatch.IsMatch`, again by title only. OMDb often lists a film a year late, which is why the second lookup exists.

On 2026-11-26, OMDb returned these (checked by hand):

| Lookup | Result | Votes |
|---|---|---|
| `t=Under the Skin&y=2013` | tt4698852, "Under the Skin" (2013) | 16 |
| `t=Under the Skin` | tt1441395, Jonathan Glazer's, listed as 2014 | 173,619 |
| `t=Filth` | tt1450321, listed as 2014 | 123,308 |
| `t=The Wife` | tt3750872, listed as 2018 | 45,386 |

`imdbVotes` is a string with thousands separators, such as `"952,400"`, or `"N/A"`.

## Goals / Non-Goals

**Goals:** stop saving obscure same-title films, and find the intended film where OMDb's title-only lookup returns it.

**Non-Goals:** finding the intended film in other cases, re-checking saved results, and a configurable threshold.

## Decisions

- **Vote threshold rather than director names.** Comparing directors would need a director in the LLM's output, plus matching that copes with accents, co-directors ("Joel Coen, Ethan Coen" against "the Coen brothers") and name forms. A wrong director from the LLM would silently drop a correct film. A vote count is one number OMDb already returns, and in every wrong match seen the count was far below that of any real suggestion.
- **Check votes as part of each lookup's match, not once after both.** A low-vote result then counts as no match, so the existing title-only lookup runs. That recovers all three Glasgow films for at most one extra request, which is the same request a year mismatch triggers today. Checking only the final result would just drop them.
- **1,000 votes, as a constant in the OMDb client.** The owner chose 1,000 on 2026-11-26. A setting would add configuration with no use for it yet, and changing a constant is a one-line change.
- **Parse with `NumberStyles.AllowThousands` and `InvariantCulture`.** OMDb always uses commas as thousands separators, whatever the server's culture. `"N/A"`, an empty value and anything unreadable count as 0, which makes them too few, consistent with how ratings treat unreadable values.
- **Votes are not stored.** They are needed only to decide the match. Storing them would mean a column that is never shown and never refreshed.
- **OMDb's own `Response: "True"` with too few votes is not a failure.** It does not throw `MovieInfoUnavailableException`, so the result is not partial, the same as for a title or year mismatch.

Alternatives rejected:
- **`s=` search, then pick the most-voted result:** needs one `i=` request per candidate to get vote counts, against OMDb's 1,000-request daily free limit.
- **Ask the LLM for IMDb IDs:** LLMs invent plausible-looking IDs, and verifying them still needs the same check.

## Risks / Trade-offs

- [A genuinely obscure local film is dropped] → accepted. The app is for travel inspiration, so well-known films matter most, and a dropped film looks the same as a suggestion OMDb could not find.
- [A recent release has under 1,000 votes in its first weeks] → accepted. It can be found once the City's result is regenerated, which only happens when the saved result is deleted.
- [Some fake OMDb responses in unit tests have no `imdbVotes`, so existing tests would see every match rejected] → give the shared test response a high vote count by default.

## Migration Plan

No schema change. After deploying, delete Glasgow's saved result (GeoNames 2648579) from the development database: its FilmingLocations and the Movies no other City uses, with `GeneratedAt` reset. The next search regenerates it. Nothing is deployed yet, so no other environment is affected.
