# How does free-text input resolve to one canonical City?

Type: grilling
Status: resolved
Blocked by: 

## Question

Users can pick from a dropdown or type a custom city ("paris", "Paris, France", "Paris, Texas", "Londres"). How is input normalised to a single City (LLM, geocoding API, string rules)? What is a City's identity in the database: keep the Name+Country composite key from `Entities.md`, or a surrogate ID? What happens with ambiguous or unrecognised input?

## Answer

Geocoding API (Open-Meteo). Users must pick a suggestion. Integer primary key plus unique GeoNames ID. Unmatched input returns no suggestions.

Details: openspec/changes/add-city-lookup/design.md
