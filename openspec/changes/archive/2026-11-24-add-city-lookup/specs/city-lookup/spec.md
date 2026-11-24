# Spec Delta

## Purpose

Lets users find one specific City from typed text or a featured list, so that later searches always refer to an unambiguous place.

## ADDED Requirements

### Requirement: Suggest Cities for typed text
The system SHALL return up to 10 matching Cities for `GET /cities?query=<text>`, each with its GeoNames ID, name, region (nullable), country and country code.

#### Scenario: Ambiguous name
- **WHEN** a client requests suggestions for "paris"
- **THEN** the response is 200 and includes Paris in France and Paris in Texas as separate Cities with different GeoNames IDs

#### Scenario: No match
- **WHEN** a client requests suggestions for text that matches no City
- **THEN** the response is 200 with an empty list

#### Scenario: City without a region
- **WHEN** a matching City has no region
- **THEN** it is returned with a null region

### Requirement: Validate suggestion queries
The system MUST reject a query that, after trimming whitespace, is missing, shorter than 2 characters or longer than 100 characters, without contacting the geocoder.

#### Scenario: Query too short
- **WHEN** a client requests suggestions for "p"
- **THEN** the response is 400 ProblemDetails and the geocoder is not contacted

#### Scenario: Whitespace only
- **WHEN** a client requests suggestions for three spaces
- **THEN** the response is 400 ProblemDetails

#### Scenario: Missing query
- **WHEN** a client requests `/cities` without a query
- **THEN** the response is 400 ProblemDetails

#### Scenario: Query too long
- **WHEN** a client requests suggestions for 101 characters
- **THEN** the response is 400 ProblemDetails

### Requirement: Report invalid queries in one format
The system SHALL report an invalid query as ProblemDetails whatever response format the client prefers.

#### Scenario: Client prefers HTML
- **WHEN** a client that accepts only HTML sends an invalid query
- **THEN** the response is 400 with content type `application/problem+json`

### Requirement: Suggest only populated places
The system MUST exclude geocoder results that are not currently populated places in their own right. Regions, landscapes, abandoned, historical or destroyed places, and sections of a city are not Cities.

#### Scenario: Section of a city
- **WHEN** the geocoder returns a city and a section of that city
- **THEN** only the city is suggested

#### Scenario: Abandoned place
- **WHEN** the geocoder returns a place marked as abandoned, historical or destroyed
- **THEN** it is not suggested

#### Scenario: Non-city result
- **WHEN** the geocoder returns a mountain alongside a city
- **THEN** only the city is suggested

### Requirement: Match names in the user's language
The system SHALL use the request's preferred language when matching and naming suggestions, and English when none is supplied.

#### Scenario: French name
- **WHEN** a client whose preferred language is French requests suggestions for "Londres"
- **THEN** London in the United Kingdom is among the suggestions

#### Scenario: No language supplied
- **WHEN** a client supplies no preferred language
- **THEN** suggestions are matched and named in English

### Requirement: Report geocoder failure
The system SHALL return 503 ProblemDetails when suggestions cannot be fetched from the geocoder.

#### Scenario: Geocoder unavailable
- **WHEN** the geocoder times out, returns an error or rejects the request because of its quota
- **THEN** the response is 503 ProblemDetails without upstream details

### Requirement: List featured Cities
The system SHALL return curated UK and European Cities from `GET /cities/featured`, ordered by country and then name, in the same shape as suggestions.

#### Scenario: Featured list
- **WHEN** a client requests featured Cities
- **THEN** the response is 200 with every featured City and no others

#### Scenario: Geocoder unavailable
- **WHEN** the geocoder is unavailable and a client requests featured Cities
- **THEN** the featured list is still returned

### Requirement: Keep City identity unique
The system MUST store at most one City per GeoNames ID.

#### Scenario: Same name, different places
- **WHEN** two Cities share a name and country but have different GeoNames IDs
- **THEN** both can be stored

#### Scenario: Duplicate identifier
- **WHEN** a second City with an existing GeoNames ID is stored
- **THEN** it is rejected

### Requirement: Suggestions do not create Cities
The system MUST NOT store Cities as a result of requesting suggestions.

#### Scenario: Suggestions leave storage unchanged
- **WHEN** a client requests suggestions for "paris"
- **THEN** the set of stored Cities is unchanged
