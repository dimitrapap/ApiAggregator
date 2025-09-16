using ApiAggregator.Models.AggregatedItem;
using ApiAggregator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiAggregator.Tests
{
    [TestClass]
    public class MetricsStoreTests
    {
        [TestMethod]
        public void Record_And_Snapshot_Computes_Totals_Averages_And_Buckets()
        {
            // Arrange
            IMetricsStore store = new InMemoryMetricsStore();

            // GitHub: 3 calls -> 50ms (fast), 150ms (avg), 250ms (slow)
            store.Record(SourceId.GitHub, 50);
            store.Record(SourceId.GitHub, 150);
            store.Record(SourceId.GitHub, 250);

            // Weather: 2 calls -> 90ms (fast), 90ms (fast)
            store.Record(SourceId.Weather, 90);
            store.Record(SourceId.Weather, 90);

            // HackerNews: 1 call -> 180ms (avg)
            store.Record(SourceId.HackerNews, 180);

            // Act
            var snapshot = store.Snapshot().ToList();

            // Assert GitHub
            var gh = snapshot.Single(s => s.Source == SourceId.GitHub);
            Assert.AreEqual(3, gh.TotalRequests);
            Assert.AreEqual((50 + 150 + 250) / 3.0, gh.AvgMs, 0.001);
            Assert.AreEqual(1, gh.FastCount);
            Assert.AreEqual(1, gh.AverageCount);
            Assert.AreEqual(1, gh.SlowCount);

            // Assert Weather
            var wx = snapshot.Single(s => s.Source == SourceId.Weather);
            Assert.AreEqual(2, wx.TotalRequests);
            Assert.AreEqual(90.0, wx.AvgMs, 0.001);
            Assert.AreEqual(2, wx.FastCount);
            Assert.AreEqual(0, wx.AverageCount);
            Assert.AreEqual(0, wx.SlowCount);

            // Assert HackerNews
            var hn = snapshot.Single(s => s.Source == SourceId.HackerNews);
            Assert.AreEqual(1, hn.TotalRequests);
            Assert.AreEqual(180.0, hn.AvgMs, 0.001);
            Assert.AreEqual(0, hn.FastCount);
            Assert.AreEqual(1, hn.AverageCount);
            Assert.AreEqual(0, hn.SlowCount);
        }
    }
}
