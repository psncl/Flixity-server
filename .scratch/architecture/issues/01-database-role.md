# What role does the database play?

Type: grilling
Status: open
Blocked by: 

## Question

Is Postgres a lazily-filled cache of Gemini+OMDb results (first search for a City populates it, later searches read from it), or a curated catalog that is the source of truth? When is stored data considered stale, and is it ever refreshed or re-generated? This shapes nearly every other ticket.
