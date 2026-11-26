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
