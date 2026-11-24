using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Common.Database;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ConnectionPoolTests(DatabaseFixture database)
{
    [Theory]
    [InlineData("", 10)]
    [InlineData(";Maximum Pool Size=5", 5)]
    [InlineData(";MaxPoolSize=5", 5)]
    public async Task PoolSize_IsSmallUnlessConfigured(string setting, int expected)
    {
        await using var api = new ApiFactory(database);
        await using var configured = api.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Database", database.ConnectionString + setting));
        using var scope = configured.Services.CreateScope();

        var connectionString = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString();

        new NpgsqlConnectionStringBuilder(connectionString).MaxPoolSize.Should().Be(expected);
    }
}
