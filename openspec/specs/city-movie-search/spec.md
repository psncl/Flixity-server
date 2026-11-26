# city-movie-search Specification

## Purpose

Lets users see Movies filmed in a chosen City, with a Blurb for each, generated on the first search and reused afterwards.

## Requirements

### Requirement: Search Movies by City
The system SHALL return the City, whether the result is partial, and up to 10 Movies in a stable order for `POST /cities/{geoNamesId}/search`.

#### Scenario: Movies found
- **WHEN** a client searches a City that has Movies
- **THEN** the response is 200 with the City, `partial` false and between 1 and 10 distinct Movies

#### Scenario: More than ten confirmed
- **WHEN** more than 10 suggested Movies can be confirmed
- **THEN** only the first 10 in suggestion order are returned

### Requirement: Describe each Movie
The system SHALL return each Movie's IMDb ID, title, year, director, poster URL, plot and Blurb. Director, poster URL and plot are null when unavailable.

#### Scenario: Missing metadata
- **WHEN** no poster is available for a Movie
- **THEN** the Movie is returned with a null poster URL

### Requirement: Include only Filming Locations
The system MUST request Movies filmed in the City, excluding those merely set there, and MUST allow fewer Movies than requested.

#### Scenario: Request to the Movie source
- **WHEN** Movies are requested for a City
- **THEN** the request identifies the City by name, region and country, asks for filmed-in only, and permits fewer results

### Requirement: Keep Blurbs short and specific
The system SHALL give each Movie a Blurb about the searched City of at most 300 characters.

#### Scenario: Overlong Blurb
- **WHEN** a generated Blurb exceeds 300 characters
- **THEN** it is shortened to at most 300 characters, at a word boundary unless that would lose more than half, without splitting a character

#### Scenario: Same Movie in two Cities
- **WHEN** one Movie is found for two Cities
- **THEN** each City shows its own Blurb for it

#### Scenario: Incomplete suggestion
- **WHEN** a suggestion has no title, year or Blurb
- **THEN** it is dropped

### Requirement: Verify suggested Movies
The system MUST drop a suggestion unless the metadata source returns a movie with a matching title, ignoring case, accents and punctuation, a year within 1 of the suggested year, and at least 1,000 IMDb votes. A missing or unreadable vote count counts as fewer than 1,000.

#### Scenario: Year differs by one
- **WHEN** "Amélie" from 2001 is suggested and the metadata source lists "Amélie" from 2002
- **THEN** the Movie is included with the year 2001

#### Scenario: Different title returned
- **WHEN** "Quantum of Solace" is suggested and only "Quantum of Solace: Director Marc Forster" is returned
- **THEN** the suggestion is dropped

#### Scenario: Year too far away
- **WHEN** "Amelie" from 2001 is suggested and only "Amelie" from 2017 is returned
- **THEN** the suggestion is dropped

#### Scenario: Special characters
- **WHEN** a suggested title contains `&` or `#`
- **THEN** the complete title is looked up

#### Scenario: Duplicate Movies
- **WHEN** two suggestions resolve to the same IMDb ID
- **THEN** the Movie is returned once, at its first position

#### Scenario: Obscure film with the same title and year
- **WHEN** "Under the Skin" from 2013 is suggested, the metadata source lists a 16-vote "Under the Skin" from 2013, and also lists a 173,619-vote "Under the Skin" from 2014
- **THEN** the Movie included is the 173,619-vote film, with the year 2013

#### Scenario: Only obscure films match
- **WHEN** every matching movie the metadata source returns has fewer than 1,000 IMDb votes
- **THEN** the suggestion is dropped, and the result is not partial

#### Scenario: Vote count unavailable
- **WHEN** a matching movie's IMDb vote count is missing or cannot be read as a number
- **THEN** it is treated as having fewer than 1,000 votes

#### Scenario: Exactly 1,000 votes
- **WHEN** a matching movie has exactly 1,000 IMDb votes
- **THEN** the Movie is included

### Requirement: Reuse completed results
The system SHALL save a complete result and serve later searches of that City without contacting any external service.

#### Scenario: Repeat search
- **WHEN** a City with a complete result is searched again while every external service is unavailable
- **THEN** the same Movies are returned in the same order

#### Scenario: Complete empty result
- **WHEN** a first search finishes without failures and no Movie survives
- **THEN** the response is 200 with no Movies, and later searches return it without contacting external services

#### Scenario: Shared Movie
- **WHEN** a Movie already stored for one City is found for another
- **THEN** it is stored once and its existing metadata is unchanged

### Requirement: Generate once for simultaneous searches
The system SHALL generate a City's result at most once when simultaneous searches arrive and generation succeeds.

#### Scenario: Two simultaneous first searches
- **WHEN** two clients search the same new City at once
- **THEN** Movies are requested once and both clients receive the same result

#### Scenario: Simultaneous searches without a saved result
- **WHEN** a search fails, is partial or finds no such City while other searches of that City wait for it
- **THEN** the waiting searches receive the same outcome, and nothing is generated again for them

#### Scenario: First client leaves
- **WHEN** the client whose search is generating the result disconnects while another waits
- **THEN** the waiting search generates the result

#### Scenario: Two Cities share a new Movie
- **WHEN** simultaneous searches of two Cities find the same new Movie
- **THEN** both searches succeed and the Movie is stored once

### Requirement: Return partial results without saving them
The system SHALL return the confirmed Movies with `partial` true, and save nothing, when a metadata lookup that could have changed the result fails for a reason other than not finding the movie.

#### Scenario: One lookup fails
- **WHEN** one lookup keeps failing after retries and others succeed
- **THEN** the response is 200 with the confirmed Movies and `partial` true, and the next search generates again

#### Scenario: Irrelevant failure
- **WHEN** 10 Movies are confirmed and a lookup of a later suggestion fails
- **THEN** the result is complete and saved

#### Scenario: Quota used up
- **WHEN** the metadata source refuses a request because of its quota
- **THEN** it is not retried, no further Movies are looked up, lookups under way can finish, and the result is partial

#### Scenario: Temporary failure
- **WHEN** a lookup fails temporarily and then succeeds on a retry
- **THEN** the result is not partial because of that lookup

#### Scenario: Metadata source unavailable
- **WHEN** every lookup fails
- **THEN** the response is 200 with no Movies and `partial` true, and nothing is saved

### Requirement: Handle Movie source failure
The system SHALL retry the Movie source once when it fails or returns unusable data, then return 503 ProblemDetails and save nothing.

#### Scenario: Retry succeeds
- **WHEN** the first attempt returns unusable data and the second succeeds
- **THEN** the search completes normally

#### Scenario: Both attempts fail
- **WHEN** both attempts fail
- **THEN** the response is 503 ProblemDetails without upstream details, and nothing is saved

### Requirement: Identify the City safely
The system MUST obtain the name of a City that is not stored from the geocoder by its GeoNames ID, never from the client.

#### Scenario: New City
- **WHEN** a client searches a City that is not stored
- **THEN** its English name, region and country come from the geocoder

#### Scenario: Invalid ID
- **WHEN** the GeoNames ID is not a positive integer
- **THEN** the response is 400 ProblemDetails and no external service is contacted

#### Scenario: Unknown ID
- **WHEN** the geocoder does not know the ID, or it is not a City
- **THEN** the response is 404 ProblemDetails and Movies are not requested

#### Scenario: Geocoder unavailable
- **WHEN** the City is not stored and the geocoder is unavailable
- **THEN** the response is 503 ProblemDetails and nothing is saved

### Requirement: Limit uncached searches per IP
The system SHALL allow 20 searches that are not served from saved results per IP address in each 60-minute window, and reject further ones with 429 ProblemDetails and a `Retry-After` of the time left. IPv6 addresses in one /64 network count as one address.

#### Scenario: Limit exceeded
- **WHEN** an IP address that has used its 20 uncached searches searches another City that is not saved
- **THEN** the response is 429 with `Retry-After`, and no external service is contacted

#### Scenario: Cached search at the limit
- **WHEN** an IP address that has used its 20 uncached searches searches a City with a saved result
- **THEN** the saved result is returned

#### Scenario: Cached searches are not counted
- **WHEN** an IP address makes more than 20 searches of a City with a saved result
- **THEN** all succeed and none count towards the limit

#### Scenario: Unsuccessful search
- **WHEN** an uncached search is partial, fails, or is for an unknown City
- **THEN** it counts towards the limit

#### Scenario: Waiting for another search
- **WHEN** a search waits for a simultaneous search of the same City and receives its result or outcome
- **THEN** the waiting search does not count towards the limit

#### Scenario: Other endpoints
- **WHEN** an IP address has reached the limit
- **THEN** city suggestions and featured Cities still work for it

#### Scenario: Time left
- **WHEN** a client is rejected 59 minutes into its window
- **THEN** `Retry-After` is at most 60 seconds

#### Scenario: Same IPv6 network
- **WHEN** a client uses several addresses in one /64 network
- **THEN** their uncached searches share one limit

#### Scenario: Different IP address
- **WHEN** one IP address has reached the limit
- **THEN** an uncached search from another IP address is allowed

### Requirement: Switch the Movie source by configuration
The system SHALL select the LLM provider and model by configuration, and MUST refuse to start with an unsupported provider.

#### Scenario: Different model
- **WHEN** the configured model is changed
- **THEN** the next generated search uses that model without code changes

#### Scenario: Unsupported provider
- **WHEN** an unsupported provider is configured
- **THEN** the app does not start and names the invalid setting

### Requirement: Retire the previous movie endpoints
The system SHALL NOT serve `GET /movies` or `POST /movies/search`.

#### Scenario: Old endpoints
- **WHEN** a client calls either previous endpoint
- **THEN** the response is 404
