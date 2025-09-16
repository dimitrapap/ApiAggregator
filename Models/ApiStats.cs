using ApiAggregator.Models.AggregatedItem;

namespace ApiAggregator.Models
{
    public record ApiStats(
    SourceId Source,
    long TotalRequests,
    double AvgMs,
    long FastCount,     // <100ms
    long AverageCount,  // 100–200ms
    long SlowCount      // >200ms
);
}
