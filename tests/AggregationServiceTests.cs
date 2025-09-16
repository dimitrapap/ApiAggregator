using ApiAggregator.Models.AggregatedItem;
using ApiAggregator.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ApiAggregator.Tests
{

    // Simple fake implementing IExternalClient
    file sealed class FakeClient : IExternalClient
    {
        private readonly Func<string?, CancellationToken, Task<List<AggregatedItem>>> _behavior;
        public SourceId Source { get; }

        public FakeClient(SourceId source, Func<string?, CancellationToken, Task<List<AggregatedItem>>> behavior)
        {
            Source = source;
            _behavior = behavior;
        }

        public Task<List<AggregatedItem>> FetchAsync(string? query, CancellationToken ct) => _behavior(query, ct);
    }


    [TestClass]
    public sealed class AggregationServiceTests
    {
        private static AggregatedItem Item(string title, SourceId src, string dateIso, double? score, string url = "u")
            => new AggregatedItem(title: title, url: url, source: src,
                                  date: string.IsNullOrWhiteSpace(dateIso) ? null : DateTimeOffset.Parse(dateIso),
                                  score: score);

        private static AggregatedItem Item1(string title, SourceId src) =>
            new AggregatedItem(title: title, url: "u", source: src, date: null, score: null);

        MemoryCache cache = new MemoryCache(new MemoryCacheOptions());

        [TestMethod]
        public async Task AggregateAsync_MergesResults_And_DefaultSortsByDateDesc()
        {
            // Arrange: two clients return one item each
            var c1 = new FakeClient(SourceId.GitHub, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item("A", SourceId.GitHub, "2025-01-01", 10) }));

            var c2 = new FakeClient(SourceId.HackerNews, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item("B", SourceId.HackerNews, "2025-06-01", 20) }));

            var svc = new AggregationService(new[] { c1, c2 }, cache);

            // Act
            var resp = await svc.AggregateAsync(
                q: "blazor", sortBy: null, order: null, from: null, to: null, sources: null, ct: CancellationToken.None);

            // Assert
            Assert.AreEqual(2, resp.Items.Count, "Should merge items from both clients");
            Assert.AreEqual("B", resp.Items[0].Title, "Default sort by date desc should place newer first");
            Assert.AreEqual(0, resp.Errors.Count, "No errors expected");
        }

        [TestMethod]
        public async Task AggregateAsync_SortsByScore_Ascending()
        {
            var c1 = new FakeClient(SourceId.GitHub, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> {
                    Item("A", SourceId.GitHub, "2025-01-01", 50),
                    Item("B", SourceId.GitHub, "2025-01-02", 10)
                }));

            var svc = new AggregationService(new[] { c1 }, cache);

            var resp = await svc.AggregateAsync(
                q: "x", sortBy: "score", order: "asc", from: null, to: null, sources: null, ct: CancellationToken.None);

            Assert.AreEqual(2, resp.Items.Count);
            Assert.AreEqual("B", resp.Items[0].Title, "Lowest score first when order=asc");
        }

        [TestMethod]
        public async Task AggregateAsync_FiltersByDateRange()
        {
            var c1 = new FakeClient(SourceId.Weather, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> {
                    Item("Old", SourceId.Weather, "2024-01-01", 1),
                    Item("InRange", SourceId.Weather, "2025-05-01", 2),
                    Item("TooNew", SourceId.Weather, "2026-01-01", 3)
                }));

            var svc = new AggregationService(new[] { c1 }, cache);

            var from = DateTimeOffset.Parse("2025-01-01");
            var to = DateTimeOffset.Parse("2025-12-31");

            var resp = await svc.AggregateAsync(
                q: null, sortBy: "date", order: "asc", from: from, to: to, sources: null, ct: CancellationToken.None);

            Assert.AreEqual(1, resp.Items.Count, "Should keep only items within [from,to]");
            Assert.AreEqual("InRange", resp.Items[0].Title);
        }

        [TestMethod]
        public async Task AggregateAsync_ContinuesOnClientError_And_ReportsInErrors()
        {
            var ok = new FakeClient(SourceId.GitHub, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item("OK", SourceId.GitHub, "2025-01-01", 1) }));

            var boom = new FakeClient(SourceId.HackerNews, (q, ct) =>
                    throw new InvalidOperationException("HN is down"));

            var svc = new AggregationService(new[] { ok, boom }, cache);

            var resp = await svc.AggregateAsync(
                q: "x", sortBy: null, order: null, from: null, to: null, sources: null, ct: CancellationToken.None);

            Assert.AreEqual(1, resp.Items.Count, "Should return partial results from healthy clients");
            Assert.AreEqual(1, resp.Errors.Count, "Should include error for failing client");
            StringAssert.Contains(resp.Errors[0], "HackerNews");
        }

        [TestMethod]
        public async Task Sources_Filter_Keeps_Only_Selected_Sources_Repeated_Params()
        {
            // Arrange: 3 fake clients
            var gh = new FakeClient(SourceId.GitHub, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("GH", SourceId.GitHub) }));

            var wx = new FakeClient(SourceId.Weather, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("WX", SourceId.Weather) }));

            var hn = new FakeClient(SourceId.HackerNews, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("HN", SourceId.HackerNews) }));

            var svc = new AggregationService(new IExternalClient[] { gh, wx, hn }, cache);

            // Act: ask only for GitHub + HackerNews via repeated query parameters
            var sources = new[] { "GitHub", "HackerNews" };
            var resp = await svc.AggregateAsync(
                q: "x", sortBy: null, order: null,
                from: null, to: null,
                sources: sources,
                ct: CancellationToken.None);

            // Assert: only GH + HN items
            var titles = resp.Items.Select(i => i.Title).ToList();
            CollectionAssert.AreEquivalent(new[] { "GH", "HN" }, titles);
        }

        [TestMethod]
        public async Task Sources_Filter_Parses_CommaSeparated_Tokens()
        {
            // Arrange
            var gh = new FakeClient(SourceId.GitHub, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("GH", SourceId.GitHub) }));
            var wx = new FakeClient(SourceId.Weather, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("WX", SourceId.Weather) }));
            var hn = new FakeClient(SourceId.HackerNews, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("HN", SourceId.HackerNews) }));

            var svc = new AggregationService(new IExternalClient[] { gh, wx, hn }, cache);

            // Act: comma-separated single param
            var sources = new[] { "GitHub,HackerNews" };
            var resp = await svc.AggregateAsync(
                q: "x", sortBy: null, order: null,
                from: null, to: null,
                sources: sources,
                ct: CancellationToken.None);

            // Assert
            var titles = resp.Items.Select(i => i.Title).ToList();
            CollectionAssert.AreEquivalent(new[] { "GH", "HN" }, titles);
        }

        [TestMethod]
        public async Task Sources_Null_Or_Empty_Returns_All()
        {
            // Arrange
            var gh = new FakeClient(SourceId.GitHub, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("GH", SourceId.GitHub) }));
            var wx = new FakeClient(SourceId.Weather, (q, ct) =>
                Task.FromResult(new List<AggregatedItem> { Item1("WX", SourceId.Weather) }));

            var svc = new AggregationService(new IExternalClient[] { gh, wx }, cache);

            // Act: no sources filter
            var resp = await svc.AggregateAsync(
                q: "x", sortBy: null, order: null,
                from: null, to: null,
                sources: null,
                ct: CancellationToken.None);

            // Assert: both included
            var titles = resp.Items.Select(i => i.Title).ToList();
            CollectionAssert.AreEquivalent(new[] { "GH", "WX" }, titles);
        }
    }
}

