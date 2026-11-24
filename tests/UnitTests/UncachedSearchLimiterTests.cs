using System.Net;
using FluentAssertions;

namespace server.Features.Movies.Tests;

public class UncachedSearchLimiterTests
{
    private static readonly IPAddress Address = IPAddress.Parse("10.0.0.1");

    private readonly FakeTime _time = new();
    private readonly UncachedSearchLimiter _limiter;

    public UncachedSearchLimiterTests() => _limiter = new UncachedSearchLimiter(_time);

    [Fact]
    public void RetryAfter_IsTheTimeLeftInTheWindow()
    {
        UseAll(Address);
        _time.Now += TimeSpan.FromMinutes(59);

        _limiter.TryAcquire(Address, out var retryAfter).Should().BeFalse();

        retryAfter.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void NewWindow_AllowsSearchesAgain()
    {
        UseAll(Address);
        _time.Now += TimeSpan.FromMinutes(60);

        UseAll(Address);
        _limiter.TryAcquire(Address, out var retryAfter).Should().BeFalse();
        retryAfter.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void RejectedSearches_DoNotExtendTheWindow()
    {
        UseAll(Address);
        _time.Now += TimeSpan.FromMinutes(30);
        _limiter.TryAcquire(Address, out _).Should().BeFalse();
        _time.Now += TimeSpan.FromMinutes(30);

        _limiter.TryAcquire(Address, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("10.0.0.1", "10.0.0.2", false)]
    [InlineData("10.0.0.1", "::ffff:10.0.0.1", true)]
    [InlineData("2001:db8:1:1::1", "2001:db8:1:1:ffff:ffff:ffff:ffff", true)]
    [InlineData("2001:db8:1:1::1", "2001:db8:1:2::1", false)]
    public void AddressesOfOneClient_ShareALimit(string first, string second, bool shared)
    {
        UseAll(IPAddress.Parse(first));

        _limiter.TryAcquire(IPAddress.Parse(second), out _).Should().Be(!shared);
    }

    [Fact]
    public void UnknownAddresses_ShareALimit()
    {
        UseAll(null);

        _limiter.TryAcquire(null, out _).Should().BeFalse();
    }

    [Fact]
    public void ExpiredClients_AreForgotten()
    {
        for (byte i = 0; i < 100; i++) _limiter.TryAcquire(new IPAddress([10, 0, 0, i]), out _);
        _time.Now += TimeSpan.FromMinutes(60);

        _limiter.TryAcquire(IPAddress.Parse("10.1.0.1"), out _);

        _limiter.Count.Should().Be(1);
    }

    private void UseAll(IPAddress? address)
    {
        for (var i = 0; i < UncachedSearchLimiter.Limit; i++)
            _limiter.TryAcquire(address, out _).Should().BeTrue();
    }

    private class FakeTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
