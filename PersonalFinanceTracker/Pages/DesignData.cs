namespace PersonalFinanceTracker.Pages
{
    public static class DesignData
    {
        public static List<KeyValuePair<string, decimal>> TrendSample { get; } =
            new()
            {
                new("2025-05", 1000),
                new("2025-06", 2000),
                new("2025-07", 1800),
                new("2025-08", 2300)
            };
    }
}
