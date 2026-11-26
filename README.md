# Flixity server

## Local setup

You need the .NET SDK (10 or later), the .NET 10 and ASP.NET Core 10 runtimes, and Docker. Run these from the repository root.

1. Start Postgres and wait until it is ready:

   ```powershell
   docker compose up -d --wait
   ```

2. Store the connection string in user secrets. This password is for the local container only:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5433;Database=flixity;Username=flixity;Password=flixity-local" --project src/server.csproj
   ```

3. Store the API keys. City lookup works without them, but searching a new city needs both:

   ```powershell
   dotnet user-secrets set "OPENAI_API_KEY" "your-key" --project src/server.csproj
   dotnet user-secrets set "OMDB_API_KEY" "your-key" --project src/server.csproj
   ```

4. Install the EF Core tool and apply migrations:

   ```powershell
   dotnet tool restore
   dotnet ef database update --project src/server.csproj
   ```

5. Run the API:

   ```powershell
   dotnet run --project src/server.csproj --launch-profile https
   ```

## Settings

`Llm:Provider` and `Llm:Model` in `src/appsettings.json` choose the LLM. `OpenAI` is the only provider so far. To add one, add its package and choose its `IChatClient` in `AddLlmClient` (`src/ConfigureServices.cs`).

`Wikidata:UserAgent` in `src/appsettings.json` is sent with each Wikidata request. Wikidata needs no key, but its [User-Agent policy](https://foundation.wikimedia.org/wiki/Policy:User-Agent_policy) asks for the app's name and a way to contact you, such as a URL or email. Without the setting, a default with no contact is used. Wikidata supplies each movie's Rotten Tomatoes, Metacritic, Letterboxd and MUBI IDs for its links. If it is unavailable, searches still work, with search links instead.

The first search of a city costs money (one LLM call) and OMDb quota. Its result is saved permanently. To search a city again from scratch, delete its `FilmingLocations` and set its `GeneratedAt` to null.

## Tests

```powershell
dotnet test tests/UnitTests
dotnet test tests/IntegrationTests
```

Integration tests start a temporary Postgres container, so Docker must be running.
