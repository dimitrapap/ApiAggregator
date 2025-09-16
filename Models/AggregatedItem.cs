namespace ApiAggregator.Models.AggregatedItem
{
    public enum SourceId 
    { 
        GitHub, 
        Weather,
        HackerNews
    }
    public class AggregatedItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public SourceId Source { get; set; }
        public DateTimeOffset? Date { get; set; }
        public double? Score { get; set; }

        public AggregatedItem(string title, string url, SourceId source, DateTimeOffset? date, double? score)
        {
            Title = title;
            Url = url;
            Source = source;
            Date = date;
            Score = score;
        }

        // μπορείς να φτιάξεις και default ctor αν θες να χρησιμοποιήσεις object initializers
        public AggregatedItem() { }
    }

    public class AggregationResponse
    {
        public List<AggregatedItem> Items { get; set; }
        public List<string> Errors { get; set; }

        public AggregationResponse() { }

        public AggregationResponse(List<AggregatedItem> items, List<string> errors)
        {
            Items = items;
            Errors = errors;
        }
    }
}
