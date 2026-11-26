using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using server.Common.Api;
using server.Features.Movies;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class WikidataConfigurationTests(DatabaseFixture database)
{
    [Fact]
    public async Task SiteIdClient_IsWikidata()
    {
        await using var api = new ApiFactory(database);
        using var scope = api.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMovieSiteIdClient>().Should().BeOfType<MovieSiteIdWikidataClient>();
    }

    [Fact]
    public async Task ConfiguredUserAgent_IsUsed()
    {
        await using var api = new ApiFactory(database);
        await using var configured = api.WithWebHostBuilder(builder =>
            builder.UseSetting("Wikidata:UserAgent", "MyApp/2.0 (https://example.test)"));

        configured.Services.GetRequiredService<IOptions<WikidataOptions>>().Value.UserAgent
            .Should().Be("MyApp/2.0 (https://example.test)");
    }

    [Fact]
    public async Task WithoutTheSetting_DefaultUserAgentIsUsed()
    {
        await using var api = new ApiFactory(database);

        api.Services.GetRequiredService<IOptions<WikidataOptions>>().Value.UserAgent
            .Should().Be(new WikidataOptions().UserAgent);
    }

    [Fact]
    public async Task EmptyUserAgent_StopsStartup_AndIsNamed()
    {
        await using var api = new ApiFactory(database);
        await using var configured = api.WithWebHostBuilder(builder => builder.UseSetting("Wikidata:UserAgent", " "));

        var act = () => configured.CreateClient();

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Wikidata:UserAgent");
    }
}
