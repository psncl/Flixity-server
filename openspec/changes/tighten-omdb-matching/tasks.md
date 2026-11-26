# Tasks

## 1. Vote threshold

- [x] 1.1 Read `imdbVotes` in the OMDb client and reject a match with fewer than 1,000 votes in each lookup, so that the lookup without the year still runs; give the shared fake OMDb response a high vote count by default. Verify with unit tests for every new scenario in the spec: an obscure same-year film replaced by the title-only result, only obscure films giving no match without an exception, `"N/A"`, empty and unreadable counts rejected, exactly 1,000 kept, `"952,400"` read as 952,400, and no second request when the first result passes
- [x] 1.2 Add `tighten-omdb-matching` to the build order in `CLAUDE.md`'s Planning section; verify all unit and integration tests pass

## 2. Real-service check

- [ ] 2.1 Delete Glasgow's saved result (GeoNames 2648579) from the development database: its FilmingLocations and the Movies no other City uses, with `GeneratedAt` reset. Verify that Liverpool's result and the shared The Batman are untouched, then search Glasgow again with the real services and verify that Under the Skin, Filth and The Wife are now tt1441395, tt1450321 and tt3750872, or were dropped

## Workflow follow-up

- Archive the change once the owner has reviewed the Glasgow result, and list it as done in `CLAUDE.md`'s Planning section.
