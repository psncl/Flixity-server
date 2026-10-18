# Schema migrations for Dapper + Npgsql (Postgres) on Azure App Service

Research date: 2026-10-18. Context: Flixity backend, .NET 10 minimal API, Dapper (no EF Core), Postgres, Azure App Service, low traffic, single developer learning .NET.

Dapper only maps query results to objects. It has no idea what the schema looks like, so something else has to create and evolve tables. All five options below do the same core job: keep a list of change scripts in source control, record which ones have run in a "journal"/"version" table in the database, and run the ones that haven't.

## Summary table

| | DbUp | FluentMigrator | Evolve | grate | Plain SQL scripts (hand-rolled) |
|---|---|---|---|---|---|
| Migrations written in | SQL (embedded resources, files) or C# scripts | C# classes (fluent API), raw SQL possible | SQL (Flyway-style file names) | SQL in convention folders | SQL |
| Latest release (NuGet) | dbup-postgresql 7.0.1, 2026-02-23; dbup-core 6.1.1 | FluentMigrator 8.0.1, 2026-01-21 | 3.2.0 stable (2023-06-30); 3.4.0-alpha1 (2025-09-30) | grate 2.1.6, 2026-07-29 | n/a |
| Targets | net8.0 + netstandard2.0 | netstandard2.0 + net48 | netstandard2.0 (stable); net9.0 (alpha) | grate.postgresql: net8.0, net9.0, **net10.0** | n/a |
| Runs .NET 10? | Yes (net8.0/netstandard2.0 assets load on net10) | Yes (netstandard2.0) | Stable yes via netstandard2.0, but stale | Yes, explicit net10.0 target | Yes |
| Runs from | Your code (console app or app startup) | In-process runner, `dotnet fm` CLI, Migrate.exe | Library, .NET tool, standalone CLI | Standalone CLI/exe, dotnet tool, Docker, or library via DI | Whatever you write |
| Rollback (down) | No: forward-only | Yes: `Down()` methods, `dotnet fm rollback --steps/--version` | Not offered for versioned scripts | No: "there's no `down`!" | Whatever you write |
| Postgres | Yes (`dbup-postgresql`) | Yes (`FluentMigrator.Runner.Postgres`, `.AddPostgres()`) | Yes | Yes (`grate.postgresql`) | Yes |
| Learning curve | Lowest of the libraries | Medium (new C# DSL + DI runner) | Low (Flyway naming) | Medium (many folders/script types) | Low to start, grows as you reinvent journaling |

## Option by option

### DbUp

- **What it is:** a .NET library that "tracks which SQL scripts have been run already, and runs the change scripts that are needed to get your database up to date." Typical setup is a console app: embed `.sql` files as `EmbeddedResource`, build an upgrader with `DeployChanges.To...().WithScriptsEmbeddedInAssembly(...).LogToConsole().Build()` and call `PerformUpgrade()`. ([DbUp docs](https://dbup.readthedocs.io/en/latest/))
- **Postgres:** separate provider package `dbup-postgresql` ("PostgreSQL provider for DbUp"). ([repo](https://github.com/DbUp/dbup-postgresql), [NuGet](https://www.nuget.org/packages/dbup-postgresql))
- **Maintenance:** dbup-postgresql 7.0.1 published 2026-02-23 targeting net8.0 and netstandard2.0; dbup-core 6.1.1 the same day. Main repo last pushed 2026-02-23, not archived. ([NuGet registration API](https://api.nuget.org/v3/registration5-gz-semver2/dbup-postgresql/index.json), [GitHub](https://github.com/DbUp/DbUp))
- **How it runs:** it's just code you call. You can run it at app startup, or as a small separate console project run from the deploy pipeline. The docs also suggest a class library embedded in a maintenance screen. ([DbUp docs](https://dbup.readthedocs.io/en/latest/))
- **Rollback:** none. The docs describe only forward operations (`PerformUpgrade`, `GetScriptsToExecute`, `MarkAsExecuted`) ([usage](https://dbup.readthedocs.io/en/latest/usage/)), and the philosophy page argues for transitions: "I don't tell the database what the state is - I tell it how to get there." ([philosophy](https://dbup.readthedocs.io/en/latest/philosophy-behind-dbup/)) To undo something you write a new forward script.

### FluentMigrator

- **What it is:** migrations are C# classes with a version attribute and `Up()`/`Down()` methods. You define both "to enable rolling back migrations when needed". ([home](https://fluentmigrator.github.io/))
- **Postgres:** `.AddPostgres()` on the runner builder, listed provider. ([quick start](https://fluentmigrator.github.io/intro/quick-start.html))
- **Maintenance:** most active of the group. FluentMigrator 8.0.1 released 2026-01-21 (8.0.0 2026-01-14, 7.2.0 2025-12-02), targeting netstandard2.0 + net48. Repo pushed 2026-10-16. ([NuGet registration API](https://api.nuget.org/v3/registration5-gz-semver2/fluentmigrator.runner.postgres/index.json), [releases](https://github.com/fluentmigrator/fluentmigrator/releases))
- **How it runs:** the docs recommend the **in-process runner** for most apps: `AddFluentMigratorCore().ConfigureRunner(rb => rb.AddPostgres().WithGlobalConnectionString(...).ScanIn(asm).For.All())`, then `IMigrationRunner.MigrateUp()`. There's also a global tool `FluentMigrator.DotNet.Cli` (`dotnet fm migrate -p ... -c ... -a MyApp.dll`) and a console Migrate.exe. ([quick start](https://fluentmigrator.github.io/intro/quick-start.html), [runners](https://fluentmigrator.github.io/runners/index.html))
- **Rollback:** the only option here with built-in rollback: `dotnet fm rollback ... --version 20200701000000` or `--steps 3`. ([dotnet-fm](https://fluentmigrator.github.io/runners/dotnet-fm.html)) The catch: a `Down()` that drops a column can't bring the data back, so rollback is mostly useful in development.
- **Learning curve:** you learn a C# schema DSL on top of SQL. With Dapper you write SQL anyway, so this adds a second way to describe the schema.

### Evolve

- **What it is:** Flyway-inspired, "uses plain SQL scripts"; "Every time you run your project, it will automatically ensure that your database is up-to-date." Offered as a .NET library, a .NET tool and a standalone CLI. Supports PostgreSQL, SQL Server, MySQL/MariaDB, SQLite, Cassandra, CockroachDB. ([repo](https://github.com/lecaillon/Evolve), [docs](https://evolve-db.netlify.app/))
- **Maintenance: the weakest.** The README calls it "a personal, free-time project with no funding". The last stable NuGet release is 3.2.0 (2023-06-30). A 3.4.0-alpha1 targeting net9.0 shipped 2025-09-30, and the last commit is the same day ("Add .Net9 support"). No commits since. No .NET 10 target. ([GitHub commits API](https://api.github.com/repos/lecaillon/Evolve/commits), [NuGet registration API](https://api.nuget.org/v3/registration5-gz-semver2/evolve/index.json))
- **Rollback:** none mentioned in the README or docs home. It follows the forward-only Flyway model.

### grate

- **What it is:** a SQL-script migrator, "largely backwards compatible with RoundhousE", now under the `grate-devs` org. ([repo](https://github.com/grate-devs/grate)) Scripts go in convention folders that run in a fixed order: `createDatabase`, `beforeMigration`, `alterDatabase`, `runBeforeUp`, **`up`** (one-time), `runFirstAfterUp`, `functions`, `views`, `sprocs`, `triggers`, `indexes`, `permissions`, `afterMigration`, and so on. Each folder is one of three script types: one-time, anytime (re-run when the file changes) and everytime. ([getting started](https://grate-devs.github.io/grate/getting-started/), [script types](https://grate-devs.github.io/grate/script-types/))
- **Postgres:** supported (SQL Server, PostgreSQL, MariaDB/MySQL, SQLite, Oracle). ([docs](https://grate-devs.github.io/grate/))
- **Maintenance:** active. grate 2.1.6 released 2026-07-29, repo pushed 2026-10-13. `grate.postgresql` 2.1.6 explicitly targets net8.0, net9.0 and **net10.0**. ([releases](https://github.com/grate-devs/grate/releases), [NuGet registration API](https://api.nuget.org/v3/registration5-gz-semver2/grate.postgresql/index.json))
- **How it runs:** mainly as an external tool: self-contained binaries (Windows/Linux/macOS), `dotnet tool install -g grate`, Docker image `grate-devs/grate`, winget, Homebrew. ([getting grate](https://grate-devs.github.io/grate/getting-grate/)) The repo has CI examples for GitHub Actions, Azure DevOps and k8s ([examples](https://github.com/grate-devs/grate/tree/main/examples)). It can also run in-process: `grate.postgresql` has `services.AddGrateWithPostgreSQL(config)` ([source](https://github.com/grate-devs/grate/blob/main/src/grate.postgresql/DependencyInjection/RegistrationExtensions.cs)), but the docs site doesn't document library use.
- **Rollback:** none. The docs say of the `up` folder: "there's no `down`!" ([getting started](https://grate-devs.github.io/grate/getting-started/))
- **Learning curve:** writing SQL is easy, but the folder/script-type model has a lot of surface area, built for teams with lots of views, sprocs and permissions.

### Plain SQL scripts (no library)

- You keep numbered `.sql` files and either (a) run them by hand with `psql`, or (b) write your own runner that keeps a `schema_version` table. That runner is what DbUp/Evolve already are. You'd have to build the journal table, ordering, per-script transactions and failure handling yourself.
- Fine for a first table or two, but you end up rebuilding a worse DbUp. The one real benefit is zero dependencies.

## Startup vs separate deploy step (Azure App Service)

Microsoft's guidance (written for EF Core, but it's about the pattern, not EF):

- "We recommend that production apps **not** call `Database.Migrate` at application startup... If the app is scaled out to multiple server instances, it's hard to ensure database schema updates don't happen from multiple servers." ([ASP.NET Core docs](https://learn.microsoft.com/aspnet/core/data/ef-rp/migrations?view=aspnetcore-10.0#applying-migrations-in-production))
- Startup migration "can be acceptable for applications that prefer simple deployment"; a separate step "is still preferred when review, least-privilege credentials, coordinated rollout, or high availability is important." It also recommends a separate deployment identity with schema permissions, and a runtime identity that only reads and writes data. ([EF Core: Applying Migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying))

EF Core 9+ takes a database-wide lock so concurrent startup migrations are safe ([same page](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying#migration-locking)). With these Dapper-era tools, don't assume you get that lock unless the tool documents it. On a single App Service instance (this project's low-traffic case) that doesn't matter. It matters once you scale out or use deployment slots, which start a second copy of the app against the same database.

In practice:
- **Startup (simplest):** call DbUp / FluentMigrator / grate-in-process from `Program.cs` before `app.Run()`. One deploy, nothing extra in CI. The app's DB user needs DDL rights.
- **Separate step (more "correct"):** run a small migrator console project, `dotnet fm`, or the grate CLI/Docker image in the GitHub Actions / Azure DevOps job before deploying the app.
- The same DbUp code can do both: put it in a small method and call it either from startup or from a `--migrate` console entry point.

## Recommendation

**Use DbUp with `dbup-postgresql`, plain `.sql` files embedded in the assembly, run at app startup for now.**

- It fits Dapper: you already write SQL, so migrations are SQL too. There's no second schema DSL to learn (unlike FluentMigrator).
- It's maintained (7.0.1, Feb 2026), works on .NET 10 via net8.0/netstandard2.0, and has the smallest API of the five: about ten lines of builder code.
- Startup execution is acceptable on a single low-traffic App Service instance. Move it to a pipeline step later (same code, console entry point) if you add scale-out or slots, or want a lower-privilege runtime DB user.
- Accept forward-only: undo with a new script, and rely on Azure Postgres backups / point-in-time restore for disasters.

Runner-up: **FluentMigrator** if real `Down()` rollback matters to you, or **grate** if you'd rather run migrations as a CLI/Docker step in CI from day one (it's the only one with an explicit net10.0 target). **Avoid Evolve** (no stable release since 2023, no commits since Sept 2025). Avoid **hand-rolled scripts** beyond a throwaway prototype.
