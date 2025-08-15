using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.Data
{
    public class SeedDataService
    {
        private readonly RecordRepository _recordRepository;
        private readonly AccountRepository _accountRepository;
        private readonly BookRepository _bookRepository;
        private readonly ILogger<SeedDataService> _logger;

        private readonly string _seedDataFilePath = "SeedData.json";

        // Legacy name-based preference key (kept for backward compatibility)
        private const string PrefKeyCurrentBook = "current_book";
        // New id-based preference key
        private const string PrefKeyCurrentBookId = "current_book_id";

        public SeedDataService(
            RecordRepository recordRepository,
            AccountRepository accountRepository,
            BookRepository bookRepository,
            ILogger<SeedDataService> logger)
        {
            _recordRepository = recordRepository;
            _accountRepository = accountRepository;
            _bookRepository = bookRepository;
            _logger = logger;
        }

        /// <summary>
        /// Legacy entry point: resolve current book from preferences and seed that book.
        /// </summary>
        public async Task LoadSeedDataAsync()
        {
            // Prefer id-based key; fallback to legacy name-based key
            int bookId = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (bookId <= 0)
            {
                var legacyName = Preferences.Default.Get(PrefKeyCurrentBook, "默认");
                var book = await _bookRepository.EnsureBookAsync(
                    string.IsNullOrWhiteSpace(legacyName) ? "默认" : legacyName.Trim());
                bookId = book.Id;

                // persist id for future runs
                Preferences.Default.Set(PrefKeyCurrentBookId, bookId);
            }

            await LoadSeedDataAsync(bookId);
        }

        /// <summary>
        /// Seed data into the given book (by bookId).
        /// </summary>
        public async Task LoadSeedDataAsync(int bookId)
        {
            try
            {
                // Ensure the physical table exists for this book
                await _recordRepository.EnsureTableAsync(bookId);

                await using Stream recordStream = await FileSystem.OpenAppPackageFileAsync(_seedDataFilePath);

                // NOTE: Ensure JSON date format matches Record.Timestamp type.
                var records = await JsonSerializer.DeserializeAsync<List<Record>>(recordStream);
                if (records is null || records.Count == 0)
                {
                    _logger.LogInformation("No seed records found in {SeedFile}.", _seedDataFilePath);
                    return;
                }

                _logger.LogInformation("Loaded {Count} seed records for bookId={BookId}.", records.Count, bookId);

                foreach (var record in records)
                {
                    // 1) Resolve or create account by display name to get AccountId
                    var displayAccount = (record.Account ?? "默认账户").Trim();
                    var acc = await _accountRepository.AddAccountAsync(displayAccount); // returns existing or creates new

                    // 2) Ensure essential fields
                    record.AccountId = acc.Id;                    // use ID-based link
                    record.Account = acc.Name;                  // keep display name
                    if (record.Timestamp == default)
                        record.Timestamp = DateTime.Now;          // fallback if JSON missing

                    // 3) Persist into the specific book's table
                    await _recordRepository.SaveAsync(bookId, record);
                }

                _logger.LogInformation("Seed data import finished for bookId={BookId}.", bookId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error loading Record seed data for bookId={BookId}", bookId);
            }
        }

        /// <summary>
        /// Convenience overload: resolves/creates a book by name, then seeds it.
        /// </summary>
        public async Task LoadSeedDataByBookNameAsync(string bookName)
        {
            var book = await _bookRepository.EnsureBookAsync(
                string.IsNullOrWhiteSpace(bookName) ? "默认" : bookName.Trim());
            await LoadSeedDataAsync(book.Id);
        }
    }
}
