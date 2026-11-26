# movie-links-and-ratings Specification

## Purpose

Lets users open each Movie on IMDb, Letterboxd, Rotten Tomatoes, Metacritic and MUBI, and see the ratings available for it.

## Requirements

### Requirement: Link each Movie to five sites
The system SHALL return `links` with every Movie in a City search result: one link each for IMDb, Letterboxd, Rotten Tomatoes, Metacritic and MUBI, in that order. Each link has `site`, `url`, `rating` and `isSearch`.

#### Scenario: Movie in a result
- **WHEN** a City search returns a Movie
- **THEN** the Movie has exactly five links, in the order IMDb, Letterboxd, Rotten Tomatoes, Metacritic, MUBI

#### Scenario: Saved result
- **WHEN** a City with a saved result is searched again while every external service is unavailable
- **THEN** each Movie has the same links and ratings as in the first response

### Requirement: Link directly when the site's ID is known
The system SHALL link to the Movie's own page on a site when it knows the Movie's ID there, with `isSearch` false.

#### Scenario: IMDb
- **WHEN** a Movie with IMDb ID `tt0137523` is returned
- **THEN** its IMDb link is `https://www.imdb.com/title/tt0137523/` with `isSearch` false

#### Scenario: Known site IDs
- **WHEN** the Rotten Tomatoes ID `m/fight_club`, Metacritic ID `movie/fight-club` and MUBI ID `918` are known
- **THEN** the links are `https://www.rottentomatoes.com/m/fight_club`, `https://www.metacritic.com/movie/fight-club` and `https://mubi.com/films/918`, each with `isSearch` false

#### Scenario: Letterboxd ID known
- **WHEN** the Letterboxd ID `fight-club` is known
- **THEN** the Letterboxd link is `https://letterboxd.com/film/fight-club/` with `isSearch` false

#### Scenario: Letterboxd ID unknown
- **WHEN** no Letterboxd ID is known for IMDb ID `tt0137523`
- **THEN** the Letterboxd link is `https://letterboxd.com/imdb/tt0137523/` with `isSearch` false

### Requirement: Fall back to a site search
The system SHALL link to the site's search for the Movie's title, with `isSearch` true, when it does not know the Movie's Rotten Tomatoes, Metacritic or MUBI ID.

#### Scenario: IDs unknown
- **WHEN** no site IDs are known for "Fight Club"
- **THEN** the Rotten Tomatoes, Metacritic and MUBI links are `https://www.rottentomatoes.com/search?search=Fight%20Club`, `https://www.metacritic.com/search/Fight%20Club/` and `https://mubi.com/en/search/films?query=Fight%20Club`, each with `isSearch` true

#### Scenario: Special characters
- **WHEN** a search link is built for a title containing `&`, `#`, `/` or `?`
- **THEN** the complete title is encoded so that the search is for the whole title

### Requirement: Show available ratings
The system SHALL return the IMDb rating as `x.x/10` and the Metascore as `n/100`, and a null rating when it is unavailable. Rotten Tomatoes, Letterboxd and MUBI ratings SHALL always be null.

#### Scenario: All ratings available
- **WHEN** a Movie has IMDb rating 8.8 and Metascore 67, and its Rotten Tomatoes ID is known
- **THEN** the IMDb and Metacritic ratings are `8.8/10` and `67/100`, and Rotten Tomatoes, Letterboxd and MUBI have null ratings

#### Scenario: Rating unavailable
- **WHEN** the metadata source has no Metascore for a Movie
- **THEN** its Metacritic link has a null rating and is still returned

#### Scenario: Unreadable rating
- **WHEN** the metadata source returns a rating that cannot be read as a number
- **THEN** that rating is null and the Movie is still returned

### Requirement: Keep links and ratings from the first search
The system SHALL save a Movie's site IDs and ratings when the Movie is first stored, and SHALL NOT refresh them.

#### Scenario: Shared Movie
- **WHEN** a Movie stored for one City is found for another
- **THEN** its links and ratings are the saved ones, and its site IDs are not looked up again

### Requirement: Keep searches working when site IDs cannot be found
The system SHALL return and save a City's result when looking up site IDs fails, using search links for the missing IDs and without marking the result partial. Missing IDs SHALL NOT be looked up again later.

#### Scenario: ID source unavailable
- **WHEN** the site ID lookup fails after its retry during a first search
- **THEN** the response is 200 with `partial` unaffected, the Movies have search links for Rotten Tomatoes, Metacritic and MUBI, and the result is saved

#### Scenario: Temporary failure
- **WHEN** the site ID lookup fails temporarily and succeeds on a retry
- **THEN** the Movies have direct links for the IDs found

#### Scenario: Movie unknown to the ID source
- **WHEN** the ID source has no entry for a Movie's IMDb ID
- **THEN** that Movie has search links for Rotten Tomatoes, Metacritic and MUBI, and the other Movies are unaffected

#### Scenario: Request to the ID source
- **WHEN** site IDs are looked up for a first search
- **THEN** one request covers all new Movies in the result, identifies the app with a descriptive User-Agent, and sends no API key
