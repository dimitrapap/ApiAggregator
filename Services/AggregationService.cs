using ApiAggregator.Models.AggregatedItem;
using Microsoft.Extensions.Caching.Memory;

namespace ApiAggregator.Services
{
    public class AggregationService
    {
        private readonly IEnumerable<IExternalClient> _clients;
        private readonly IMemoryCache _cache;

        public AggregationService(IEnumerable<IExternalClient> clients, IMemoryCache cache)
        {
            _clients = clients;
            _cache = cache;
        }

        public async Task<AggregationResponse> AggregateAsync(string? q, string? sortBy, string? order, DateTimeOffset? from, DateTimeOffset? to, string[]? sources, CancellationToken ct)
        {
            // --- Φτιάξε canonical key για caching ---
            static string CanonicalSources(string[]? srcs)
                => (srcs is null || srcs.Length == 0)
                    ? "ALL"
                    : string.Join(",", srcs
                        .SelectMany(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Select(s => s.Trim().ToUpperInvariant())
                        .OrderBy(s => s)); // σταθερή σειρά

            var cacheKey = $"agg::{q ?? ""}::{from?.UtcDateTime:o}::{to?.UtcDateTime:o}::{(sortBy ?? "date")}::{(order ?? "desc")}::{CanonicalSources(sources)}";

            if (_cache.TryGetValue(cacheKey, out AggregationResponse? cached))
                return cached!;


            // Parse sources (case-insensitive), δέχεται "GitHub", "Weather", "HackerNews"
            HashSet<SourceId>? wanted = null;
            if (sources is { Length: > 0 })
            {
                wanted = new HashSet<SourceId>();
                foreach (var s in sources)
                {
                    // υποστήριξε και comma-separated "GitHub,Weather"
                    foreach (var token in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        if (Enum.TryParse<SourceId>(token, ignoreCase: true, out var id))
                            wanted.Add(id);
                }
                if (wanted.Count == 0) wanted = null; // αν τίποτα δεν “έπιασε”, αγνόησέ το
            }

            var clientsToUse = (wanted is null)
                ? _clients
                : _clients.Where(c => wanted.Contains(c.Source));

            var tasks = clientsToUse.Select(async c =>
            {
                try
                {
                    var items = await c.FetchAsync(q, ct);
                    return (items: items, err: (string?)null);
                }
                catch (Exception ex)
                {
                    return (items: new List<AggregatedItem>(), err: $"{c.Source}: {ex.Message}");
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            var all = tasks.SelectMany(t => t.Result.items).ToList();
            var errors = tasks.Select(t => t.Result.err).Where(e => e is not null)!.ToList()!;

            // ---- Filtering ----
            if (from is not null)
                all = all.Where(i => i.Date is not null && i.Date >= from).ToList();

            if (to is not null)
                all = all.Where(i => i.Date is not null && i.Date <= to).ToList();

            // ---- Sorting ----
            // defaults: sortBy = "date", order = "desc"
            var key = (sortBy ?? "date").ToLowerInvariant();
            var desc = !string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase);

            all = key switch
            {
                "score" => desc
                    ? all.OrderByDescending(i => i.Score ?? double.MinValue).ToList()
                    : all.OrderBy(i => i.Score ?? double.MinValue).ToList(),

                "date" or _ => desc
                    ? all.OrderByDescending(i => i.Date ?? DateTimeOffset.MinValue).ToList()
                    : all.OrderBy(i => i.Date ?? DateTimeOffset.MinValue).ToList()
            };

            var resp = new AggregationResponse(all, errors);

            // --- Cache set (30s absolute, 15s sliding) ---
            _cache.Set(cacheKey, resp, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromSeconds(15),
                Size = all.Count // προαιρετικά, αν ενεργοποιήσεις size limit
            });

            return resp;

        }
    }
}
