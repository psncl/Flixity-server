using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace server.Features.Movies;

public class SearchCityMoviesHandler
{
    public static async Task<Results<Ok<SearchCityMoviesResponse>, ProblemHttpResult>> Handle(
        [FromRoute, Range(1, int.MaxValue)] int geoNamesId,
        HttpContext context, CityMovieStore store, CitySearchLocks locks, UncachedSearchLimiter limiter,
        ICityGeocoder geocoder, IMovieInfoLlmClient llmClient, CityMovieFinder finder,
        IMovieSiteIdClient siteIdClient, ILogger<SearchCityMoviesHandler> logger,
        CancellationToken cancellationToken)
    {
        if (await store.GetAsync(geoNamesId, cancellationToken) is { } cached) return TypedResults.Ok(cached);

        using var cityLock = await locks.AcquireAsync(geoNamesId, cancellationToken);

        // Another search may have finished while this one waited for the lock.
        if (await store.GetAsync(geoNamesId, cancellationToken) is { } completed) return TypedResults.Ok(completed);
        if (cityLock.OutcomeWhileWaiting is { } waitedFor) return ToResult(waitedFor);

        if (!limiter.TryAcquire(context.Connection.RemoteIpAddress, out var retryAfter))
        {
            context.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many searches of new cities. Try again later.");
        }

        var outcome = await GenerateAsync(geoNamesId, store, geocoder, llmClient, finder, siteIdClient, logger,
            cancellationToken);
        if (outcome is not null)
        {
            cityLock.Share(outcome);
            return ToResult(outcome);
        }

        // Read it back, so that first and repeat searches return exactly the same.
        return TypedResults.Ok((await store.GetAsync(geoNamesId, cancellationToken))!);
    }

    /// <returns>Null if the result was saved.</returns>
    private static async Task<UnsavedOutcome?> GenerateAsync(int geoNamesId, CityMovieStore store,
        ICityGeocoder geocoder, IMovieInfoLlmClient llmClient, CityMovieFinder finder,
        IMovieSiteIdClient siteIdClient, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var city = await store.GetCityAsync(geoNamesId, cancellationToken) ??
                       await geocoder.GetByIdAsync(geoNamesId, cancellationToken);
            if (city is null) return new UnsavedOutcome(StatusCodes.Status404NotFound);

            var suggestions = await llmClient.GetMoviesFilmedInAsync(city, cancellationToken);
            var found = await finder.FindAsync(suggestions, cancellationToken);

            // Only new Movies: stored ones keep the site IDs saved with them. Returns no IDs if Wikidata fails,
            // which only means search links, so the result is still complete.
            var imdbIds = found.Movies.Select(f => f.Movie.ImdbId).ToList();
            var stored = await store.GetMoviesAsync(imdbIds, cancellationToken);
            var siteIds = await siteIdClient.FindSiteIdsAsync(
                imdbIds.Where(id => !stored.ContainsKey(id)).ToList(), cancellationToken);

            // Saved results are permanent, so an incomplete one must not be saved.
            if (found.Partial) return new UnsavedOutcome(StatusCodes.Status200OK, PartialResponse(city, found.Movies));

            await store.SaveAsync(city, found.Movies, siteIds, cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is GeocoderUnavailableException or LlmUnavailableException)
        {
            logger.LogError(ex, "Movie search failed for City {GeoNamesId}", geoNamesId);
            return new UnsavedOutcome(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static Results<Ok<SearchCityMoviesResponse>, ProblemHttpResult> ToResult(UnsavedOutcome outcome)
    {
        if (outcome.Partial is not null) return TypedResults.Ok(outcome.Partial);

        return TypedResults.Problem(statusCode: outcome.StatusCode,
            title: outcome.StatusCode == StatusCodes.Status404NotFound
                ? "City not found."
                : "Movie search is temporarily unavailable.");
    }

    private static SearchCityMoviesResponse PartialResponse(CityInfo city, IReadOnlyList<FoundMovie> found) => new(
        new SearchCityMoviesResponse.CityDetails(city.GeoNamesId, city.Name, city.Region, city.Country,
            city.CountryCode),
        Partial: true,
        found.Select(f => new SearchCityMoviesResponse.MovieDetails(f.Movie.ImdbId, f.Movie.Title, f.Movie.Year,
            f.Movie.Director, f.Movie.PosterUrl, f.Movie.Plot, f.Blurb)).ToList());
}
