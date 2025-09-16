using ApiAggregator.Models.AggregatedItem;

namespace ApiAggregator.Services
{
    public interface IExternalClient
    {
        SourceId Source { get; }
        Task<List<AggregatedItem>> FetchAsync(string? query, CancellationToken ct);
    }
}
