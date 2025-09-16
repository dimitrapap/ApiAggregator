using ApiAggregator.Models;
using ApiAggregator.Models.AggregatedItem;
using System.Collections.Concurrent;

namespace ApiAggregator.Services
{
    public class InMemoryMetricsStore : IMetricsStore
    {
        private sealed class Counter
        {
            public long Total;
            public long SumMs;
            public long Fast;
            public long Avg;
            public long Slow;
        }

        private readonly ConcurrentDictionary<SourceId, Counter> _map = new();

        public void Record(SourceId source, long elapsedMs)
        {
            var c = _map.GetOrAdd(source, _ => new Counter());
            Interlocked.Increment(ref c.Total);
            Interlocked.Add(ref c.SumMs, elapsedMs);

            if (elapsedMs < 100) Interlocked.Increment(ref c.Fast);
            else if (elapsedMs <= 200) Interlocked.Increment(ref c.Avg);
            else Interlocked.Increment(ref c.Slow);
        }

        public IEnumerable<ApiStats> Snapshot()
        {
            foreach (var (src, c) in _map)
            {
                var total = Volatile.Read(ref c.Total);
                var sumMs = Volatile.Read(ref c.SumMs);

                yield return new ApiStats(
                    Source: src,
                    TotalRequests: total,
                    AvgMs: total == 0 ? 0 : (double)sumMs / total,
                    FastCount: Volatile.Read(ref c.Fast),
                    AverageCount: Volatile.Read(ref c.Avg),
                    SlowCount: Volatile.Read(ref c.Slow)
                );
            }
        }
    }
}
