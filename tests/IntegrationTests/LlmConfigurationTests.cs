using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class LlmConfigurationTests(DatabaseFixture database)
{
    [Theory]
    [InlineData("gpt-6-luna")]
    [InlineData("another-model")]
    public async Task ConfiguredModel_IsUsed(string model)
    {
        await using var api = new ApiFactory(database);
        await using var configured = api.WithWebHostBuilder(builder => builder
            .UseSetting("Llm:Model", model)
            .UseSetting("OPENAI_API_KEY", "fake"));

        var chat = configured.Services.GetRequiredService<IChatClient>();

        chat.GetService<ChatClientMetadata>()!.DefaultModelId.Should().Be(model);
        chat.GetService<ChatClientMetadata>()!.ProviderName.Should().Be("openai");
    }

    [Fact]
    public async Task DefaultModel_IsLuna()
    {
        await using var api = new ApiFactory(database);
        await using var configured = api.WithWebHostBuilder(builder => builder.UseSetting("OPENAI_API_KEY", "fake"));

        configured.Services.GetRequiredService<IChatClient>()
            .GetService<ChatClientMetadata>()!.DefaultModelId.Should().Be("gpt-6-luna");
    }

    [Theory]
    [InlineData("Llm:Provider", "Unknown")]
    [InlineData("Llm:Model", "")]
    public async Task InvalidSetting_StopsStartup_AndIsNamed(string setting, string value)
    {
        await using var api = new ApiFactory(database);
        await using var configured = api.WithWebHostBuilder(builder => builder.UseSetting(setting, value));

        var act = () => configured.CreateClient();

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain(setting);
    }
}
