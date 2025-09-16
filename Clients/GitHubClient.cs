using ApiAggregator.Models.AggregatedItem;
using ApiAggregator.Services;
using System.Diagnostics;

namespace ApiAggregator.Clients
{
    public class GitHubClient : IExternalClient
    {
        private readonly HttpClient _http;
        private readonly IMetricsStore _metrics;
        public SourceId Source => SourceId.GitHub;

        public GitHubClient(HttpClient http, IMetricsStore metrics) 
        { 
            _http = http;
            _metrics = metrics;
        } 

        private sealed record GhResp(List<GhItem> items);
        private sealed record GhItem(string full_name, string html_url, double? score, DateTimeOffset? created_at);

        public async Task<List<AggregatedItem>> FetchAsync(string? query, CancellationToken ct)
        {
            var q = string.IsNullOrWhiteSpace(query) || query!.Contains(',') ? "aspnet core" : query.Trim();
            var sw = Stopwatch.StartNew();
            GhResp? resp = null;
            try
            {
                resp = await _http.GetFromJsonAsync<GhResp>(
                $"search/repositories?q={Uri.EscapeDataString(q)}&sort=stars&order=desc&per_page=10", ct);

                return resp?.items?.Select(i => new AggregatedItem(
                title: i.full_name,
                url: i.html_url,
                source: SourceId.GitHub,
                date: i.created_at,
                score: i.score
                )).ToList() ?? new();
            }
            finally
            {
                sw.Stop();
                _metrics.Record(Source, (long)sw.Elapsed.TotalMilliseconds);
            }
            

            
        }
    }
}
