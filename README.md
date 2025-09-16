API Aggregation Service (.NET 9)

This project is a .NET 9 API Aggregator.
It fetches data from multiple external APIs (GitHub, Open-Meteo Weather, HackerNews) and exposes them through a single unified API endpoint.

It demonstrates:

ASP.NET Core Minimal API

HttpClientFactory for external API integration

Parallelism (Task.WhenAll) for concurrent fetching

IMemoryCache for caching

InMemoryMetricsStore for request statistics

🔹 What it does

Aggregates results from 3 sources:

GitHub → repositories by search term

Weather (Open-Meteo) → current temperature & wind for coordinates

HackerNews → articles matching a search term

Supports filtering and sorting

Provides a /stats endpoint with performance metrics

Uses caching to avoid redundant calls

Handles errors gracefully → returns partial results + error messages

🚀 Getting Started
Prerequisites

.NET 9 SDK

Visual Studio 2022 or dotnet CLI

Run
dotnet run --project ApiAggregator


You’ll see something like:

Now listening on: http://localhost:5051
Now listening on: https://localhost:7051

🌐 Endpoints
Health check
GET /health


Response:

{ "status": "ok" }

Aggregated data
GET /aggregate
  ?q=string                # search term (e.g. "blazor") for GitHub/HN, or "lat,lon" for Weather
  &sortBy=date|score       # default: date
  &order=asc|desc          # default: desc
  &from=YYYY-MM-DD         # filter from date
  &to=YYYY-MM-DD           # filter to date
  &sources=GitHub          # optional, can repeat or comma-separated (e.g. sources=GitHub,HackerNews)


Examples:

Repositories & articles for blazor, weather in Athens:

http://localhost:5051/aggregate?q=blazor


Only GitHub + HackerNews:

http://localhost:5051/aggregate?q=ai&sources=GitHub,HackerNews


Weather in Athens (coordinates):

http://localhost:5051/aggregate?q=37.98,23.72


Only results within 2025, sorted by score:

http://localhost:5051/aggregate?q=ai&from=2025-01-01&to=2025-12-31&sortBy=score&order=desc


Response:

{
  "items": [
    {
      "title": "dotnet/aspnetcore",
      "url": "https://github.com/dotnet/aspnetcore",
      "source": "GitHub",
      "date": "2025-01-01T12:00:00Z",
      "score": 99.5
    },
    {
      "title": "Temp 30°C, Wind 10 km/h",
      "url": "https://open-meteo.com/en/docs",
      "source": "Weather",
      "date": "2025-01-01T15:00:00Z",
      "score": 30.0
    }
  ],
  "errors": []
}

Stats
GET /stats


Response:

[
  {
    "source": "GitHub",
    "totalRequests": 12,
    "avgMs": 134.2,
    "fastCount": 5,
    "averageCount": 4,
    "slowCount": 3
  },
  {
    "source": "Weather",
    "totalRequests": 12,
    "avgMs": 95.3,
    "fastCount": 12,
    "averageCount": 0,
    "slowCount": 0
  }
]

🛠️ Architecture

IExternalClient → contract for each external API client

Clients → GitHubClient, WeatherClient, HackerNewsClient (typed HttpClient)

AggregationService → runs all clients in parallel, merges, filters, sorts, caches

IMetricsStore → thread-safe in-memory counters (totals, avg ms, buckets), exposed via /stats

Program.cs → DI setup and endpoints