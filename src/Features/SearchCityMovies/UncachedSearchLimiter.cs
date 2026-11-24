using System.Net;
using System.Net.Sockets;

namespace server.Features.Movies;

/// <summary>
/// Limits searches that contact external services, because those cost money and quota.
/// Not ASP.NET Core's rate limiting middleware: that runs before we know whether a search is cached.
/// Not .NET's FixedWindowRateLimiter: its Retry-After is always the whole window, not the time left.
/// </summary>
public class UncachedSearchLimiter(TimeProvider timeProvider)
{
    public const int Limit = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(60);

    private class Client
    {
        public required DateTimeOffset WindowStart { get; init; }
        public int Searches { get; set; }
    }

    private readonly Dictionary<string, Client> _clients = [];
    private DateTimeOffset _lastCleanup = timeProvider.GetUtcNow();

    /// <summary>The number of clients remembered.</summary>
    public int Count
    {
        get
        {
            lock (_clients) return _clients.Count;
        }
    }

    /// <returns>False if the limit is reached. Then retryAfter says how long to wait.</returns>
    public bool TryAcquire(IPAddress? address, out TimeSpan retryAfter)
    {
        lock (_clients)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);

            var key = GetKey(address);
            if (!_clients.TryGetValue(key, out var client) || now - client.WindowStart >= Window)
                _clients[key] = client = new Client { WindowStart = now };

            retryAfter = client.WindowStart + Window - now;
            if (client.Searches == Limit) return false;

            client.Searches++;
            return true;
        }
    }

    // Once per window, so that old clients don't fill the memory.
    private void RemoveExpired(DateTimeOffset now)
    {
        if (now - _lastCleanup < Window) return;
        _lastCleanup = now;

        foreach (var (key, client) in _clients)
            if (now - client.WindowStart >= Window)
                _clients.Remove(key);
    }

    private static string GetKey(IPAddress? address)
    {
        if (address is null) return "unknown";

        // The same client can appear as 1.2.3.4 and as ::ffff:1.2.3.4.
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();

        // One IPv6 client normally has a whole /64 network of addresses: the first 8 bytes.
        return Convert.ToHexString(address.GetAddressBytes(), 0, 8);
    }
}
