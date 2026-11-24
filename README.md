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

3. Install the EF Core tool and apply migrations:

   ```powershell
   dotnet tool restore
   dotnet ef database update --project src/server.csproj
   ```

4. Run the API:

   ```powershell
   dotnet run --project src/server.csproj --launch-profile https
   ```

## Tests

```powershell
dotnet test tests/UnitTests
dotnet test tests/IntegrationTests
```

Integration tests start a temporary Postgres container, so Docker must be running.
