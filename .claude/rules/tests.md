---
paths:
  - "tests/**"
---

# Test conventions

- Stack: xUnit **v2** (2.9.x), FluentAssertions, NSubstitute. Use v2 APIs only: `IAsyncLifetime` returns `Task`, and there is no `TestContext`.
- Never call real external APIs (OpenAI, OMDb, Open-Meteo, Wikidata), not even in integration tests.
- Name tests `Subject_Outcome` or as a plain statement of behaviour: `MissingMetadata_IsNull`, `StopsAfterTenMovies`. No `Test` or `Should` prefixes.
- Separate arrange, act and assert with blank lines, not comments. Put small factory helpers (`Movie(...)`, `City(...)`, `Client(...)`) at the bottom of the class.
- Assert with FluentAssertions (`.Should()`), not `Assert.*`.

## Unit tests (`tests/UnitTests`)

- HTTP clients: pass a `FakeHttpHandler` with canned JSON (`FakeHttpHandler.Json(...)`), and check what was sent through `handler.Requests`. This is where JSON parsing, query encoding and HTTP failures are tested.
- Other dependencies: an NSubstitute substitute, or a small hand-written fake class when a test needs to control timing or record calls.

## Integration tests (`tests/IntegrationTests`, need Docker)

- Tests that use the database go in `[Collection(DatabaseCollection.Name)]` and take `DatabaseFixture`: one Postgres container per test run, with migrations applied.
- The database is shared, so each test cleans up: either open a transaction and never commit it, or delete what it saved in `DisposeAsync`. Use GeoNames IDs from `900_000_001` up for test Cities so they can't clash with the seeded featured Cities.
- To test a constraint, call `db.ChangeTracker.Clear()` before the save that should fail, so that Postgres rejects it and not EF Core.
- Endpoint tests use `ApiFactory`, which runs the real API in memory against the test database. Replace external clients (`ICityGeocoder`, `IMovieInfoLlmClient`, `IMovieInfoClient`, ...) with substitutes through `ConfigureTestServices`. Their HTTP and JSON handling is covered by unit tests, not here.
