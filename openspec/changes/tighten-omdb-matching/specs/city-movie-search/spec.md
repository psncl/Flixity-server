# Spec Delta

## MODIFIED Requirements

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
