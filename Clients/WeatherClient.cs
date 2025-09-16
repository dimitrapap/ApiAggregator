using ApiAggregator.Models.AggregatedItem;
using ApiAggregator.Services;
using System.Diagnostics;
using System.Net.Http.Json;

namespace ApiAggregator.Clients
{
    public class WeatherClient : IExternalClient
    {
        private readonly HttpClient _http;
        private readonly IMetricsStore _metrics;
        public SourceId Source => SourceId.Weather;

        public WeatherClient(HttpClient http, IMetricsStore metrics)
        {
            _http = http;
            _metrics = metrics;
        }

        private sealed record WeatherResp(CurrentWeather current_weather);
        private sealed record CurrentWeather(double temperature, double windspeed, string time);

        public async Task<List<AggregatedItem>> FetchAsync(string? query, CancellationToken ct)
        {
            // query μπορεί να είναι "lat,lon" (π.χ. "37.98,23.72"). Αλλιώς default: Αθήνα.
            (double lat, double lon) = ParseLatLonOrDefault(query);
            var url = $"v1/forecast?latitude={lat}&longitude={lon}&current_weather=true";

            var sw = Stopwatch.StartNew();
            try
            {
                var resp = await _http.GetFromJsonAsync<WeatherResp>(url, ct);
                if (resp?.current_weather is null) return new();

                var cw = resp.current_weather;
                return new()
            {
                new AggregatedItem(
                    title: $"Temp {cw.temperature}°C, Wind {cw.windspeed} km/h",
                    url: "https://open-meteo.com/en/docs",
                    source: SourceId.Weather,
                    date: DateTimeOffset.Parse(cw.time),
                    score: cw.temperature
                )
            };
            }
            finally
            {
                sw.Stop();
                _metrics.Record(Source, (long)sw.Elapsed.TotalMilliseconds);
            }

            
        }

        private static (double lat, double lon) ParseLatLonOrDefault(string? q)
        {
            if (!string.IsNullOrWhiteSpace(q) && q.Contains(','))
            {
                var parts = q.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && double.TryParse(parts[0], out var la) && double.TryParse(parts[1], out var lo))
                    return (la, lo);
            }
            return (37.98, 23.72); // Αθήνα
        }
    }
}
