# Things learned during this project:

- Storing secrets in ASP.NET Core

# Tradeoffs

- Google Gen AI .NET SDK instead of MS Agent Framework for simplicity

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
