# How are schema migrations run with Dapper + Postgres?

Type: research
Status: resolved
Blocked by: 

## Question

Dapper has no migrations. Compare DbUp, FluentMigrator, Evolve, Grate and plain SQL scripts for a .NET 10 + Npgsql app on Azure App Service: maturity/maintenance, how migrations run (app startup vs separate step), rollback story, learning curve.

## Answer

- **Recommendation: DbUp + `dbup-postgresql`** with plain `.sql` files embedded in the assembly, run at app startup for now. It fits Dapper because migrations stay plain SQL, it has the smallest API, and it's maintained (7.0.1, Feb 2026; net8.0/netstandard2.0 assets run on .NET 10).
- Startup migration is fine on a single low-traffic App Service instance. Microsoft prefers a separate deploy step once you scale out, use deployment slots, or want a least-privilege runtime DB user. The same DbUp code can later run from a console entry point in CI.
- Rollback: only **FluentMigrator** has real `Down()` + `dotnet fm rollback`. DbUp, Evolve and grate are forward-only: undo with a new script and rely on Postgres backups / point-in-time restore. FluentMigrator (8.0.1, Jan 2026, very active) is the pick if rollback matters, at the cost of learning a C# schema DSL.
- **grate** (2.1.6, Jul 2026, explicit net10.0 target) is the best fit for running migrations as a CLI/Docker step in CI. Its many folders and script types are overkill here.
- Avoid **Evolve**: no stable release since 3.2.0 (Jun 2023), only a net9.0 alpha, no commits since Sept 2025. Avoid hand-rolled scripts: you'd end up rebuilding a worse DbUp.

Findings: branch `research/dapper-migrations`, file `research/dapper-migrations.md`
