using ApiAggregator.Models;
using ApiAggregator.Models.AggregatedItem;

namespace ApiAggregator.Services
{
    public interface IMetricsStore
    {
        void Record(SourceId source, long elapsedMs);
        IEnumerable<ApiStats> Snapshot();
    }
}
