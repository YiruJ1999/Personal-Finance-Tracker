using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.Data
{
    public class SeedDataService
    {
        private readonly RecordRepository _recordRepository;
        private readonly AccountRepository _accountRepository;
        private readonly BookRepository _bookRepository;
        private readonly ILogger<SeedDataService> _logger;

        // App package seed file path. Ensure "SeedData.json" is included properly.
        private readonly string _seedDataFilePath = "SeedData.json";

        // Preference keys for current book (still used to SET the current book)
        private const string PrefKeyCurrentBook = "current_book";
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
        /// Always create a NEW book named "Default" (auto-suffixed if name already exists),
        /// set it as the current book, and seed accounts + records into it.
        /// </summary>
        public async Task LoadSeedDataAsync()
        {
            // Create a brand-new "Default" book (if "Default" exists, create "Default (2)", "Default (3)", ...)
            var book = await CreateNewDefaultBookAsync();

            // Persist "current book" info (id + name)
            Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
            Preferences.Default.Set(PrefKeyCurrentBook, book.Name);

            // Seed data into the newly created book
            await LoadSeedDataAsync(book.Id);
        }

        /// <summary>
        /// Seed accounts (once) and records into the specified book.
        /// This does NOT rely on Record.Account (removed). It maps seed AccountId -> real DB Account.Id.
        /// Supported JSON layouts:
        /// 1) Object with "Accounts" (optional) and "Records" arrays.
        /// 2) Legacy root array of records only (will create placeholder accounts "账户{seedId}").
        /// </summary>
        public async Task LoadSeedDataAsync(int bookId)
        {
            try
            {
                // 1) Ensure per-book physical table exists
                await _recordRepository.EnsureTableAsync(bookId);

                // 2) Load seed JSON from app package
                await using Stream recordStream = await FileSystem.OpenAppPackageFileAsync(_seedDataFilePath);
                using var doc = await JsonDocument.ParseAsync(recordStream);
                var root = doc.RootElement;

                // 3) Build mapping seedAccountId -> real DB account Id (will create accounts as needed)
                var seedToReal = await BuildAccountMapAsync(root);

                // 4) Resolve records array
                JsonElement recordsElem;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    // Legacy: root is directly an array of records
                    recordsElem = root;
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Records", out var recs))
                {
                    recordsElem = recs;
                }
                else
                {
                    _logger.LogInformation("No records found in {SeedFile}.", _seedDataFilePath);
                    return;
                }

                // 5) Insert records
                var count = 0;
                foreach (var rec in recordsElem.EnumerateArray())
                {
                    // AccountId is required (number or string)
                    if (!rec.TryGetProperty("AccountId", out var accElem))
                    {
                        _logger.LogWarning("Record skipped: missing AccountId.");
                        continue;
                    }

                    var seedAccId = ParseFlexibleInt(accElem);
                    if (seedAccId <= 0)
                    {
                        _logger.LogWarning("Record skipped: invalid AccountId.");
                        continue;
                    }

                    // If the account is not in the map (e.g., legacy file without Accounts), create a placeholder.
                    if (!seedToReal.TryGetValue(seedAccId, out var realAccId))
                    {
                        var placeholderName = $"账户{seedAccId}";
                        var acc = await _accountRepository.AddAccountAsync(placeholderName);
                        realAccId = acc.Id;
                        seedToReal[seedAccId] = realAccId;
                    }

                    // Extract other fields with safe defaults
                    string type = rec.TryGetProperty("Type", out var t) ? (t.GetString() ?? string.Empty).Trim() : string.Empty;
                    decimal amount = rec.TryGetProperty("Amount", out var a) && TryGetDecimal(a, out var amt) ? amt : 0m;
                    string category = rec.TryGetProperty("Category", out var c) ? (c.GetString() ?? string.Empty).Trim() : string.Empty;
                    string note = rec.TryGetProperty("Note", out var n) ? (n.GetString() ?? string.Empty).Trim() : string.Empty;

                    DateTime ts;
                    if (rec.TryGetProperty("Timestamp", out var d) &&
                        d.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(d.GetString(), out var parsed))
                    {
                        ts = parsed;
                    }
                    else
                    {
                        ts = DateTime.Now;
                    }

                    var record = new Record
                    {
                        AccountId = realAccId,
                        Type = type,
                        Amount = amount,
                        Category = category,
                        Note = note,
                        Timestamp = ts
                    };

                    await _recordRepository.SaveAsync(bookId, record);
                    count++;
                }

                _logger.LogInformation("Seed data import finished: {Count} records into bookId={BookId}.", count, bookId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error loading seed data for bookId={BookId}", bookId);
            }
        }

        /// <summary>
        /// Create a brand-new book named "Default". If a book with the same name exists,
        /// append a numeric suffix (Default (2), Default (3), ...) to guarantee a new one.
        /// </summary>
        private async Task<Book> CreateNewDefaultBookAsync()
        {
            const string baseName = "Default";
            var existing = await _bookRepository.ListBooksAsync();
            string nameToUse = baseName;

            // Ensure we create a NEW book by finding a non-conflicting name.
            int suffix = 2;
            while (existing.Any(b => string.Equals(b.Name, nameToUse, StringComparison.OrdinalIgnoreCase)))
            {
                nameToUse = $"{baseName} ({suffix++})";
            }

            // EnsureBookAsync should create the book if it does not exist.
            var book = await _bookRepository.EnsureBookAsync(nameToUse);

            // Ensure the per-book table schema is created.
            var table = await _bookRepository.GetTableNameByIdAsync(book.Id);
            await _bookRepository.EnsureBookTableSchemaAsync(table);

            return book;
        }

        /// <summary>
        /// Build a mapping from seed account id -> real DB Id, creating accounts as needed.
        /// If JSON has an "Accounts" array, use those names (and optional OpeningBalance).
        /// Otherwise derive unique AccountIds from records and create placeholders "账户{seedId}".
        /// </summary>
        private async Task<Dictionary<int, int>> BuildAccountMapAsync(JsonElement root)
        {
            var map = new Dictionary<int, int>();

            // Preferred: explicit Accounts section
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Accounts", out var accs) &&
                accs.ValueKind == JsonValueKind.Array)
            {
                foreach (var acc in accs.EnumerateArray())
                {
                    if (!acc.TryGetProperty("Id", out var idElem)) continue;
                    var seedId = ParseFlexibleInt(idElem);
                    if (seedId <= 0) continue;

                    var name = acc.TryGetProperty("Name", out var nameElem)
                        ? (nameElem.GetString() ?? string.Empty).Trim()
                        : string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) name = $"账户{seedId}";

                    // Optional opening balance support: use repository overload if present
                    decimal opening = 0m;
                    if (acc.TryGetProperty("OpeningBalance", out var obElem) && TryGetDecimal(obElem, out var ob))
                        opening = ob;

                    Account real;
                    try
                    {
                        // Prefer overload with opening balance if non-zero (keeps idempotency by name)
                        real = opening != 0m
                            ? await _accountRepository.AddAccountAsync(name, opening)
                            : await _accountRepository.AddAccountAsync(name);
                    }
                    catch
                    {
                        // Fallback to simple create-or-get by name
                        real = await _accountRepository.AddAccountAsync(name);
                    }

                    map[seedId] = real.Id;
                }

                return map;
            }

            // Fallback: derive from records
            JsonElement recordsElem;
            if (root.ValueKind == JsonValueKind.Array)
            {
                recordsElem = root;
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Records", out var recs))
            {
                recordsElem = recs;
            }
            else
            {
                return map;
            }

            var seen = new HashSet<int>();
            foreach (var rec in recordsElem.EnumerateArray())
            {
                if (!rec.TryGetProperty("AccountId", out var accElem)) continue;
                var seedAccId = ParseFlexibleInt(accElem);
                if (seedAccId > 0 && seen.Add(seedAccId))
                {
                    var name = $"账户{seedAccId}";
                    var real = await _accountRepository.AddAccountAsync(name);
                    map[seedAccId] = real.Id;
                }
            }

            return map;
        }

        /// <summary>
        /// Parse an int from a JsonElement that could be a number or a string. Returns 0 on failure.
        /// </summary>
        private static int ParseFlexibleInt(JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var n)) return n;
            if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), out var s)) return s;
            return 0;
        }

        /// <summary>
        /// Try to read a decimal from a JsonElement that may be number or string.
        /// </summary>
        private static bool TryGetDecimal(JsonElement elem, out decimal value)
        {
            value = 0m;
            if (elem.ValueKind == JsonValueKind.Number)
            {
                // Be permissive: accept double/int and cast to decimal.
                if (elem.TryGetDouble(out var d)) { value = (decimal)d; return true; }
                if (elem.TryGetInt64(out var l)) { value = l; return true; }
                if (elem.TryGetInt32(out var i)) { value = i; return true; }
                return false;
            }
            if (elem.ValueKind == JsonValueKind.String && decimal.TryParse(elem.GetString(), out var s))
            {
                value = s;
                return true;
            }
            return false;
        }
    }
}
