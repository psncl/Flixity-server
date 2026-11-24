using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using server.Common.Api;

namespace IntegrationTests;

/// <summary>Runs the real API in memory, with the test database and a fake geocoder.</summary>
public class ApiFactory(DatabaseFixture database, string environment = "Production") : WebApplicationFactory<Program>
{
    public ICityGeocoder Geocoder { get; } = Substitute.For<ICityGeocoder>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Database", database.ConnectionString);
        builder.ConfigureTestServices(services => services.AddSingleton(Geocoder));
    }
}
