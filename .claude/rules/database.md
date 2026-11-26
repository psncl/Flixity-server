---
paths:
  - "src/Common/Database/**"
---

# Database conventions

- Change the schema in the entity classes and `AppDbContext.OnModelCreating`, then generate the migration with the command in CLAUDE.md. Don't write migrations from scratch or edit `AppDbContextModelSnapshot.cs` by hand.
- Only edit a generated migration for what EF Core can't express, and say why in a comment. The known case: seeding a new featured City needs `INSERT ... ON CONFLICT` instead of the generated `InsertData` (see the remarks in `FeaturedCities.cs`).
- Never change a migration that is already on `main`; add a new one. To redo a migration that only exists on the current branch, use `dotnet ef migrations remove`, then generate it again.
- After generating, read the migration and check that it does what was intended (for example, a column rename should not become a drop and an add).
- Seed data uses `HasData` with fixed, negative primary keys (see `FeaturedCities.cs`): fixed so that later migrations can find the rows, negative so they never clash with the positive IDs the database generates.
- Keep the EF Core and Npgsql packages on 10.0.x: Npgsql 11 needs .NET 11.
- Cover new constraints (unique indexes, cascade deletes) with integration tests, like `CityStorageTests` and `MovieStorageTests`.
