using ApiAggregator.Clients;
using ApiAggregator.Services;

var builder = WebApplication.CreateBuilder(args);

// HttpClientFactory ��� ���� ��� clients
builder.Services.AddHttpClient<GitHubClient>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiAggregator/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
});

builder.Services.AddHttpClient<WeatherClient>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
});
builder.Services.AddHttpClient<HackerNewsClient>(client =>
{
    client.BaseAddress = new Uri("https://hn.algolia.com/");
});


// --- Expose BOTH as IExternalClient (IEnumerable<IExternalClient> will have 2 items) ---
builder.Services.AddScoped<IExternalClient>(sp => sp.GetRequiredService<GitHubClient>());
builder.Services.AddScoped<IExternalClient>(sp => sp.GetRequiredService<WeatherClient>());
builder.Services.AddScoped<IExternalClient>(sp => sp.GetRequiredService<HackerNewsClient>());

builder.Services.AddMemoryCache();
// Aggregation Service
builder.Services.AddScoped<AggregationService>();

builder.Services.AddSingleton<IMetricsStore, InMemoryMetricsStore>();



var app = builder.Build();

// ����� health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/diag", (IEnumerable<IExternalClient> clients) =>
    Results.Ok(new { count = clients.Count(), types = clients.Select(t => t.GetType().Name) }));

// ���� root ��� �� ������� ��� �� �������� �� service
app.MapGet("/", () => Results.Json(new
{
    message = "API Aggregator (.NET 9) ����� NuGet packages",
    try_aggregate = "/aggregate?q=blazor � /aggregate?q=37.98,23.72"
}));

// �� aggregation endpoint
// q: ��� GitHub ����� query string, ��� ����� "lat,lon" (�.�. "37.98,23.72")
app.MapGet("/aggregate", async (string? q, string? sortBy,string? order,DateTimeOffset? from,DateTimeOffset? to, string[]? sources, AggregationService svc, CancellationToken ct) =>
{
    var resp = await svc.AggregateAsync(q, sortBy, order, from, to, sources, ct);
    return Results.Ok(resp);
});

app.MapGet("/stats", (IMetricsStore store) => Results.Ok(store.Snapshot()));

app.Run();
