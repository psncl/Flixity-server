using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using server.Common.Api;

namespace server.Features.Movies.Tests;

public class MovieInfoLlmClientTests
{
    private static readonly CityInfo Paris = new(4717560, "Paris", "Texas", "United States", "US");

    private const string Valid = """{"movies":[{"title":" Amélie ","year":2001,"blurb":"Café des Deux Moulins."}]}""";

    private static readonly MovieSuggestion Amelie = new("Amélie", 2001, "Café des Deux Moulins.");

    [Fact]
    public async Task ReturnsSuggestions()
    {
        (await Ask(Valid)).Should().Equal(Amelie);
    }

    [Fact]
    public async Task Prompt_IdentifiesCity_AsksForFilmedInOnly_AndPermitsFewer()
    {
        var chat = new FakeChatClient(Valid);

        await Client(chat).GetMoviesFilmedInAsync(Paris);

        chat.Prompts.Should().ContainSingle().Which.Should().Contain("Paris, Texas, United States")
            .And.Contain("really filmed in the city").And.Contain("merely set there").And.Contain("Fewer than 15")
            .And.Contain("up to 15");
        chat.Options!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>()
            .Which.Schema!.Value.ToString().Should().Contain("including accents");
    }

    [Fact]
    public async Task Prompt_CityWithoutRegion_HasNoGap()
    {
        var chat = new FakeChatClient(Valid);

        await Client(chat).GetMoviesFilmedInAsync(new CityInfo(1, "Monaco", null, "Monaco", "MC"));

        chat.Prompts.Single().Should().Contain("city: Monaco, Monaco.");
    }

    [Fact]
    public async Task DropsIncompleteSuggestions()
    {
        var suggestions = await Ask("""
            {"movies":[
              null,
              {"title":"","year":2001,"blurb":"b"},
              {"title":"No year","year":null,"blurb":"b"},
              {"title":"No blurb","year":2001,"blurb":"  "},
              {"title":"Amélie","year":2001,"blurb":"Café des Deux Moulins."}
            ]}
            """);

        suggestions.Should().Equal(Amelie);
    }

    [Fact]
    public async Task EmptyList_IsAccepted()
    {
        (await Ask("""{"movies":[]}""")).Should().BeEmpty();
    }

    [Fact]
    public async Task Blurb_OfExactly300Characters_IsUnchanged()
    {
        var blurb = new string('b', 300);

        (await AskForBlurb(blurb)).Should().Be(blurb);
    }

    [Fact]
    public async Task LongBlurb_IsShortenedAtWordBoundary()
    {
        // 299 characters, then a word that crosses the limit
        var words = string.Join(" ", Enumerable.Repeat("four", 60));

        (await AskForBlurb(words + " crossing the limit")).Should().Be(words + "…").And.HaveLength(300);
    }

    [Theory]
    [InlineData(200, 150)]
    [InlineData(299, 10)]
    [InlineData(300, 10)]
    public async Task LongBlurb_KeepsOnlyWholeWords(int firstWord, int secondWord)
    {
        var blurb = await AskForBlurb(new string('a', firstWord) + " " + new string('b', secondWord));

        blurb.Length.Should().BeLessThanOrEqualTo(300);
        blurb.Should().Be(new string('a', Math.Min(firstWord, 299)) + "…");
    }

    [Fact]
    public async Task LongBlurb_WithoutSpaces_IsCut()
    {
        (await AskForBlurb(new string('b', 400))).Should().HaveLength(300).And.EndWith("b…");
    }

    [Fact]
    public async Task LongBlurb_IsShortenedAtNewline()
    {
        (await AskForBlurb(new string('a', 250) + "\n" + new string('b', 100))).Should().Be(new string('a', 250) + "…");
    }

    [Fact]
    public async Task LongBlurb_IsNotCutAtAnEarlySpace()
    {
        (await AskForBlurb("At " + new string('b', 400))).Should().HaveLength(300);
    }

    [Fact]
    public async Task LongBlurb_DoesNotSplitACharacter()
    {
        // The emoji takes two chars, at indexes 298 and 299.
        var blurb = await AskForBlurb(new string('b', 298) + "🎬" + new string('b', 100));

        blurb.Should().Be(new string('b', 298) + "…");
    }

    public static TheoryData<string> Failures => ["throw", "hang", "not json", "null", "{}"];

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FirstAttemptFails_SecondSucceeds(string failure)
    {
        var chat = new FakeChatClient(failure, Valid);

        (await Client(chat).GetMoviesFilmedInAsync(Paris)).Should().Equal(Amelie);
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task BothAttemptsFail_Throws(string failure)
    {
        var chat = new FakeChatClient(failure, failure, Valid);

        var act = () => Client(chat).GetMoviesFilmedInAsync(Paris);

        await act.Should().ThrowAsync<LlmUnavailableException>();
        chat.Prompts.Should().HaveCount(2);
    }

    [Fact]
    public async Task CancelledByCaller_IsNotRetried()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        var chat = new FakeChatClient("hang", Valid);

        var act = () => Client(chat, TimeSpan.FromSeconds(30)).GetMoviesFilmedInAsync(Paris, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        chat.Prompts.Should().ContainSingle();
    }

    [Fact]
    public void DefaultTimeout_Is60Seconds()
    {
        new LlmOptions().Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    private static async Task<string> AskForBlurb(string blurb)
    {
        var json = JsonSerializer.Serialize(new { movies = new[] { new { title = "T", year = 2000, blurb } } });
        return (await Ask(json)).Single().Blurb;
    }

    private static Task<IReadOnlyList<MovieSuggestion>> Ask(string response) =>
        Client(new FakeChatClient(response)).GetMoviesFilmedInAsync(Paris);

    private static MovieInfoLlmClient Client(IChatClient chat, TimeSpan? timeout = null) => new(
        new Lazy<IChatClient>(chat),
        Options.Create(new LlmOptions { Timeout = timeout ?? TimeSpan.FromMilliseconds(200) }),
        NullLogger<MovieInfoLlmClient>.Instance);

    /// <summary>Gives each response in turn. "throw" and "hang" simulate failures.</summary>
    private class FakeChatClient(params string[] responses) : IChatClient
    {
        public List<string> Prompts { get; } = [];
        public ChatOptions? Options { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var response = responses[Prompts.Count];
            Prompts.Add(string.Join("\n", messages.Select(message => message.Text)));
            Options = options;

            if (response == "throw") throw new HttpRequestException("LLM down");
            if (response == "hang") await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
