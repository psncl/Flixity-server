# How are Blurbs generated?

Type: grilling
Status: resolved
Blocked by: 01

## Question

Is the Blurb produced in the same Gemini call that lists Movies for a City, or a separate call per Movie+City? How do we limit hallucinated Filming Locations and keep Blurbs to 2-3 lines about the selected City? Is a Blurb generated once and stored?

## Answer

Titles and Blurbs come from one LLM call without web search, and are saved once. Prompt rules and OMDb checks reduce, but do not remove, wrong results.

Details: openspec/changes/add-city-movie-search/design.md
