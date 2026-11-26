# Things learned during this project:

- Storing secrets in ASP.NET Core
- Building the API first
- Letting go of TDD and instead treating it as a step towards TDD by writing tests after
- Some topics to write about: https://open.substack.com/pub/devleaderweekly/p/side-projects-and-soft-skills-in

# Tradeoffs

- `Microsoft.Extensions.AI` (`IChatClient`) for the LLM, instead of a provider SDK or MS Agent Framework
  - Started with the Google Gen AI .NET SDK (Gemini), chosen over MS Agent Framework for simplicity
  - Switched so the provider is a config setting behind one interface (OpenAI by default) instead of code tied to one SDK
  - Still simpler than Agent Framework, which is built for agents and workflows; this app makes one prompt-and-response call

## Vertical Slice Architecture
  - [YT: Vertical Slice Architecture: How Does it Compare to Clean Architecture](https://www.youtube.com/watch?v=T-EwN9UqRwE)
  - https://codeopinion.com/restructuring-to-a-vertical-slice-architecture/
  - https://www.architecture-weekly.com/p/my-thoughts-on-vertical-slices-cqrs
  - https://github.com/nadirbad/VerticalSliceArchitecture (Has tests)

### VSA: [YT: How to structure your .NET / C# API's by Jono Williams](https://www.youtube.com/watch?v=ZA2X1gaAhJk)

  - **Endpoint:** self-contained unit of work comprised of three things:
    1. Mapping to a path. Also providing a summary, and adding a validator.
    2. Describing the Request and Response contracts.
    3. The actual Handle logic of taking the request and generating the response. The handler can optionally be outside the endpoint (but in the same folder) according to Oskar Dudycz's article.

## Ratings as Movie columns, not a separate table

Ratings (IMDb, Rotten Tomatoes, Metacritic) and site IDs are nullable columns on Movie.

- There are three fixed sources, each with at most one value per Movie. Fixed attributes of an entity belong on it as columns; that is normal normalization.
- A ratings table would mean 0 to 3 rows per Movie, a join or `Include()` on every search, and code to turn rows back into named ratings.
- Each column keeps its own type: IMDb is a `decimal` out of 10, the others are `int` out of 100. A shared table needs one value type for all.
- Ratings are written once and never refreshed, so there is no history to keep.

A separate table would be better if sources were added often, ratings were refreshed with history, or each rating carried several fields (vote count, fetched date). Moving three columns into a table later is a small migration.
