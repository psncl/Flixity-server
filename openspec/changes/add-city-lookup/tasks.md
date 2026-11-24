# Tasks

## 1. Persistence foundation

- [ ] 1.1 Add EF Core design tooling and the Npgsql provider 10.0.x; verify `dotnet build src/server.sln` succeeds
- [ ] 1.2 Add a local Postgres container definition and document starting it and setting the connection string in user secrets; verify the documented steps connect
- [ ] 1.3 Add City and the `DbContext`, with a unique GeoNamesId, and register it with a small connection pool; verify the app starts
- [ ] 1.4 Create the initial migration; verify `dotnet ef database update` succeeds on an empty database
- [ ] 1.5 Add an integration test project using Testcontainers; verify a test applies migrations to real Postgres
- [ ] 1.6 Test duplicate GeoNames IDs and same-name Cities; verify the first is rejected and the second is stored

## 2. Geocoder client

- [ ] 2.1 Add `ICityGeocoder` and the Open-Meteo search client with URL encoding, language and a timeout; verify with unit tests using fake HTTP responses
- [ ] 2.2 Keep only populated places, limited to 10; verify with a unit test that mixes feature codes
- [ ] 2.3 Handle missing `results`, HTTP errors, quota errors and timeouts; verify unit tests distinguish no match from failure

## 3. City suggestions endpoint

- [ ] 3.1 Enable validation and ProblemDetails; verify the app builds and existing tests pass
- [ ] 3.2 Add `GET /cities` with trimming, length validation and the response shape; verify integration tests for every validation and suggestion scenario, including a missing query in Development and Production
- [ ] 3.3 Pass the preferred request language, defaulting to English; verify tests for French and for no language
- [ ] 3.4 Map geocoder failure to 503; verify the response contains no upstream details
- [ ] 3.5 Verify with an integration test that suggestions leave stored Cities unchanged

## 4. Featured Cities

- [x] 4.1 Confirm the featured list with the owner and look up each GeoNames ID; verify every ID resolves to the intended City
- [ ] 4.2 Seed featured Cities in a migration; verify they exist after migrating an empty database
- [ ] 4.3 Add `GET /cities/featured`; verify integration tests for contents, order and an unavailable geocoder

## 5. Integration check

- [ ] 5.1 Run all tests, then try both endpoints with the real geocoder for "paris", "asdfgh" and "Londres" in French; verify results match the spec
