# Planned data model

Not yet in code. The reasons are in the `design.md` files under `openspec/changes/`.

# CITY

Id (Primary key)
GeoNamesId (Unique)
Name
Region (nullable)
Country
CountryCode
IsFeatured: shown in the curated dropdown
GeneratedAt (nullable): null means never searched

# MOVIE

Id (Primary key)
ImdbId (Unique)
Title
Year
Director
Plot (nullable)
PosterUrl (nullable)
ImdbRating, RottenTomatoes, Metascore (nullable)
RottenTomatoesId, MetacriticId, LetterboxdId, MubiId (nullable)
FetchedAt

# FILMING LOCATION

CityId (FK)
MovieId (FK)
Blurb
Position: keeps the LLM's order

Primary key: Composite of CityId and MovieId
Deleting a City or Movie deletes its Filming Locations.
