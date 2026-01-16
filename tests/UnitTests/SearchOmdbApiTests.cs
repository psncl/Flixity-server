using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using server.Common.Api;

namespace server.Features.Movies.Tests;

public class SearchOmdbApiTests
{
    private readonly IMovieInfoClient _mockClient = Substitute.For<IMovieInfoClient>();

    [Fact]
    public async Task SearchingForExistentMovie_ReturnsCorrectData()
    {
        // Arrange
        SetupApiClient();
        var expectedMovie = new MovieInfo("tt0830515", "Quantum of Solace", 2008, "Marc Forster",
            "James Bond tries to stop an organisation from eliminating a country's most valuable resource.");

        // Act
        var movie = await GetMoviesHandler.Handle("quantum of solace", 2008, _mockClient);

        // Assert
        movie.Should().BeOfType<Results<Ok<MovieInfo>, NotFound>>();
        var okResult = movie.Result as Ok<MovieInfo>;
        okResult.Should().NotBeNull();
        okResult.Value.Should().BeEquivalentTo(expectedMovie);
    }

    [Fact]
    public async Task SearchingForNonexistentMovie_ReturnsNotFound()
    {
        // Arrange
        SetupApiClient();

        // Act
        var movie = await GetMoviesHandler.Handle("quantum of solace", 2009, _mockClient);

        // Assert
        movie.Should().BeOfType<Results<Ok<MovieInfo>, NotFound>>();
        var notFoundResult = movie.Result as NotFound;
        notFoundResult.Should().NotBeNull();
    }

    private void SetupApiClient()
    {
        _mockClient.GetMovieByTitleYearAsync("quantum of solace", 2008)
            .Returns(callInfo =>
            {
                return new MovieInfo("tt0830515", "Quantum of Solace", callInfo.ArgAt<int>(1), "Marc Forster",
                    "James Bond tries to stop an organisation from eliminating a country's most valuable resource.");
            });

        // Wrong year, so API call should fail
        _mockClient.GetMovieByTitleYearAsync("quantum of solace", 2009)
            .ReturnsNull();
    }
}