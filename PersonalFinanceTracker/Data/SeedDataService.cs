using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Data
{
    public class SeedDataService
    {
        private readonly RecordRepository _recordRepository;
        private readonly ILogger<SeedDataService> _logger;

        private readonly string _seedDataFilePath = "SeedData.json";

        // Use the same preference key as your PageModels
        private const string PrefKeyCurrentBook = "current_book";

        public SeedDataService(
            RecordRepository recordRepository,
            ILogger<SeedDataService> logger)
        {
            _recordRepository = recordRepository;
            _logger = logger;
        }

        /// <summary>
        /// Legacy entry point kept for backward compatibility.
        /// It resolves the current book name from preferences and seeds that book.
        /// </summary>
        public async Task LoadSeedDataAsync()
        {
            // Resolve current book from preferences (fallback to "Default")
            var bookName = Preferences.Default.Get(PrefKeyCurrentBook, "Default");
            await LoadSeedDataAsync(bookName);
        }

        /// <summary>
        /// Seed data into the per-book Record table (scheme one: one table per book).
        /// </summary>
        public async Task LoadSeedDataAsync(string bookName)
        {
            try
            {
                // Open the bundled seed JSON from app package
                await using Stream recordStream = await FileSystem.OpenAppPackageFileAsync(_seedDataFilePath);

                // NOTE: Ensure your JSON date format matches your Record.Timestamp type.
                // If Timestamp is DateTime and JSON is ISO 8601, System.Text.Json will bind automatically.
                var records = await JsonSerializer.DeserializeAsync<List<Record>>(recordStream);
                System.Diagnostics.Debug.WriteLine($"Loaded {records?.Count} records from seed data for book {bookName}");
                if (records != null && records.Count > 0)
                {
                    foreach (var record in records)
                    {
                        // Default account if missing
                        record.Account ??= "ƒ¨»œ’Àªß";

                        // IMPORTANT: save into the specific book's table
                        await _recordRepository.SaveAsync(bookName, record);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error loading Record seed data for book {BookName}", bookName);
            }
        }
    }
}
