# Data model

All of this is in code, in `src/Common/Database/`. The reasons are in the `design.md` files under `openspec/changes/` and `openspec/changes/archive/`.

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
Director (nullable)
Plot (nullable)
PosterUrl (nullable)
ImdbRating (nullable): out of 10, one decimal place
RottenTomatoes, Metascore (nullable): 0 to 100
RottenTomatoesId, MetacriticId, LetterboxdId, MubiId (nullable): site IDs, not URLs
Ratings and site IDs are saved when the Movie is first stored and never refreshed.
FetchedAt

# FILMING LOCATION

CityId (FK)
MovieId (FK)
Blurb
Position: keeps the LLM's order

Primary key: Composite of CityId and MovieId
Deleting a City or Movie deletes its Filming Locations.
