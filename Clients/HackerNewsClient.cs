using ApiAggregator.Models.AggregatedItem;
using ApiAggregator.Services;
using System.Diagnostics;

namespace ApiAggregator.Clients
{
    public class HackerNewsClient : IExternalClient
    {
        private readonly HttpClient _http;
        private readonly IMetricsStore _metrics;
        public SourceId Source => SourceId.HackerNews;

        public HackerNewsClient(HttpClient http, IMetricsStore metrics)
        {
            _http = http;
            _metrics = metrics;
        }
        // ελάχιστα DTOs για parsing
        private sealed record HnResp(HnHit[] hits);
        private sealed record HnHit(string? title, string? url, long? created_at_i, double? points);

        public async Task<List<AggregatedItem>> FetchAsync(string? query, CancellationToken ct)
        {
            var q = string.IsNullOrWhiteSpace(query) || query!.Contains(',')
                ? "technology"
                : query.Trim();

            var sw = Stopwatch.StartNew();

            try
            {
                // BaseAddress: https://hn.algolia.com/  ⇒ σχετικό URL:
                var resp = await _http.GetFromJsonAsync<HnResp>(
                    $"api/v1/search?query={Uri.EscapeDataString(q)}&hitsPerPage=10",
                    ct
                );

                var items = resp?.hits?.Select(h => new AggregatedItem(
                    title: h.title ?? "(no title)",
                    url: string.IsNullOrWhiteSpace(h.url) ? "https://news.ycombinator.com/" : h.url!,
                    source: SourceId.HackerNews,
                    date: h.created_at_i is null ? null : DateTimeOffset.FromUnixTimeSeconds(h.created_at_i.Value),
                    score: h.points
                )).ToList() ?? new();
                return items;
            }
            finally 
            {
                sw.Stop();
                _metrics.Record(Source, (long)sw.Elapsed.TotalMilliseconds);
            }

        }
    }
}
