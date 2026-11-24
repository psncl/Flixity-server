namespace server.Features.Movies;

/// <summary>
/// The result of a search that saved nothing, so it can't be read back from the database.
/// </summary>
/// <param name="StatusCode">200 for a partial result, or the error's status code</param>
/// <param name="Partial">The movies found, for a partial result</param>
public record UnsavedOutcome(int StatusCode, SearchCityMoviesResponse? Partial = null);

/// <summary>
/// Lets one search at a time generate a City's movies. Works within one app instance only.
/// </summary>
public class CitySearchLocks
{
    private class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1);
        public int Users { get; set; }

        public UnsavedOutcome? Outcome { get; set; }

        /// <summary>The number of outcomes shared so far.</summary>
        public int Shared { get; set; }
    }

    private readonly Dictionary<int, Entry> _entries = [];

    /// <summary>The number of Cities with a lock that is held or waited for.</summary>
    public int Count
    {
        get
        {
            lock (_entries) return _entries.Count;
        }
    }

    /// <returns>Dispose it to release the lock.</returns>
    public async Task<CityLock> AcquireAsync(int geoNamesId, CancellationToken cancellationToken)
    {
        Entry? entry;
        int sharedOnArrival;
        lock (_entries)
        {
            if (!_entries.TryGetValue(geoNamesId, out entry)) _entries[geoNamesId] = entry = new Entry();
            entry.Users++;
            sharedOnArrival = entry.Shared;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
        }
        catch
        {
            Leave(geoNamesId, entry);
            throw;
        }

        lock (_entries)
        {
            // An outcome from before this search arrived is out of date.
            var outcome = entry.Shared > sharedOnArrival ? entry.Outcome : null;
            return new CityLock(this, geoNamesId, entry, outcome);
        }
    }

    // Unused entries are removed, so that requests for random IDs don't fill the memory.
    private void Leave(int geoNamesId, Entry entry)
    {
        lock (_entries)
        {
            if (--entry.Users == 0) _entries.Remove(geoNamesId);
        }
    }

    public sealed class CityLock : IDisposable
    {
        private readonly CitySearchLocks _locks;
        private readonly int _geoNamesId;
        private readonly Entry _entry;

        internal CityLock(CitySearchLocks locks, int geoNamesId, object entry, UnsavedOutcome? outcome)
        {
            _locks = locks;
            _geoNamesId = geoNamesId;
            _entry = (Entry)entry;
            OutcomeWhileWaiting = outcome;
        }

        /// <summary>What another search came to while this one waited, if it saved nothing.</summary>
        public UnsavedOutcome? OutcomeWhileWaiting { get; }

        /// <summary>Give an outcome to the searches that are waiting now.</summary>
        public void Share(UnsavedOutcome outcome)
        {
            lock (_locks._entries)
            {
                _entry.Outcome = outcome;
                _entry.Shared++;
            }
        }

        public void Dispose()
        {
            _entry.Semaphore.Release();
            _locks.Leave(_geoNamesId, _entry);
        }
    }
}
