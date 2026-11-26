# Proposal

## Why

The metadata source can return an obscure film that shares a suggested Movie's title and year. Searching Glasgow on 2026-11-26 saved three such films: a 16-vote "Under the Skin" (2013) instead of Jonathan Glazer's, a 10-vote "Filth" and an unrated "The Wife". Each was shown with the wrong plot, poster, ratings and links.

## What Changes

- A found movie also needs at least 1,000 IMDb votes to count as a match. A missing or unreadable vote count counts as too few.
- A rejected match is treated like no match: the lookup without the year is tried, as today, and the suggestion is dropped if nothing matches. In the Glasgow cases, that lookup finds the intended film.
- A dropped suggestion still does not make the result partial.
- Delete Glasgow's saved development result so that it is searched again.

Not changing: title and year matching, the number of Movies returned, and saved results, which are never refreshed. Comparing director names was considered and rejected (see design.md).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `city-movie-search`: "Verify suggested Movies" adds a minimum IMDb vote count.

## Impact

- OMDb client and its unit tests: read `imdbVotes`. The fake OMDb responses in unit tests need a vote count, or every match would be rejected.
- At most one extra OMDb request per rejected match. That request already happens today whenever the lookup with the year finds no match.
- Genuinely obscure films with fewer than 1,000 votes can no longer be suggested.
- Development database: Glasgow's saved result.
