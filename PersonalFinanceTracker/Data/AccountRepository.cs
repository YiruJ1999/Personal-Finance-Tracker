using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;

namespace PersonalFinanceTracker.Data
{
    public class AccountRepository
    {
        private readonly DatabaseService _dbService;
        private readonly BookRepository _books;

        public AccountRepository(DatabaseService database, BookRepository books)
        {
            _dbService = database;
            _books = books;
        }

        // Always return a live connection; init DB if needed
        private async Task<SQLiteAsyncConnection> ConnAsync()
        {
            if (_dbService.Database == null)
                await _dbService.InitAsync();
            return _dbService.Database!;
        }

        // Create mapped table (Accounts) and migrate legacy table (Account) if needed
        public async Task EnsureDatabaseInitializedAsync()
        {
            var conn = await ConnAsync();

            // Create the table for the current model (uses [Table("Accounts")] mapping)
            await conn.CreateTableAsync<Account>();

            // Figure out mapped table and legacy table names
            var mappedAttr = typeof(Account).GetCustomAttributes(typeof(TableAttribute), false)
                                            .OfType<TableAttribute>().FirstOrDefault();
            var newTable = mappedAttr?.Name ?? "Account"; // should be "Accounts"
            var oldTable = newTable.Equals("Accounts", StringComparison.OrdinalIgnoreCase) ? "Account" : "Accounts";

            // Migrate: only when legacy table has rows and new table is empty
            var names = await conn.QueryScalarsAsync<string>("SELECT name FROM sqlite_master WHERE type='table';");
            bool hasNew = names.Contains(newTable);
            bool hasOld = names.Contains(oldTable);

            if (hasOld)
            {
                var oldCount = await conn.ExecuteScalarAsync<long>($"SELECT COUNT(1) FROM {oldTable};");
                var newCount = hasNew ? await conn.ExecuteScalarAsync<long>($"SELECT COUNT(1) FROM {newTable};") : 0L;

                if (oldCount > 0 && newCount == 0)
                {
                    // IMPORTANT: copy FROM legacy table (oldTable) INTO mapped table (newTable)
                    await conn.ExecuteAsync(
                        $"INSERT INTO {newTable} (Id, Name, Balance, CreatedAt) " +
                        $"SELECT Id, Name, Balance, CreatedAt FROM {oldTable};");

                    // Optional: drop the legacy table
                    // await conn.ExecuteAsync($"DROP TABLE IF EXISTS {oldTable};");
                }
            }

            // Unique index for case-insensitive name lookups / dedup
            await conn.ExecuteAsync(
                $"CREATE UNIQUE INDEX IF NOT EXISTS idx_{newTable}_name_nocase ON {newTable}(Name COLLATE NOCASE);");
        }

        // -------------------- Basic ops --------------------

        private async Task<Account?> GetAccountByNameAsync(string name)
        {
            var conn = await ConnAsync();
            await EnsureDatabaseInitializedAsync();
            var trimmed = name.Trim();
            return await conn.Table<Account>()
                             .Where(a => a.Name == trimmed)
                             .FirstOrDefaultAsync();
        }

        public async Task<List<Account>> ListAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();
            return await conn.Table<Account>()
                             .OrderBy(a => a.Name)
                             .ToListAsync();
        }

        public async Task<Account> AddAccountAsync(string name) =>
            await AddAccountAsync(name, 0m);

        public async Task<Account> AddAccountAsync(string name, decimal openingBalance)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Account name is required.", nameof(name));

            var conn = await ConnAsync();
            var existing = await GetAccountByNameAsync(name);
            if (existing == null)
            {
                var acc = new Account { Name = name.Trim(), Balance = openingBalance, CreatedAt = DateTime.UtcNow };
                await conn.InsertAsync(acc);
                if (acc.Id == 0)
                {
                    var rid = await conn.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                    acc.Id = (int)rid;
                }
                return acc;
            }
            else
            {
                existing.Balance = openingBalance;
                await conn.UpdateAsync(existing);
                return existing;
            }
        }

        public async Task DeleteAccountAsync(int accountId, bool alsoDeleteRecords)
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();

            await conn.DeleteAsync<Account>(accountId);

            if (alsoDeleteRecords)
            {
                var books = await _books.ListBooksAsync();
                foreach (var b in books)
                {
                    await conn.ExecuteAsync(
                        $@"DELETE FROM {BookRepository.QuoteIdent(b.TableName)} WHERE AccountId = ?;",
                        accountId);
                }
            }
        }

        public async Task UpdateAccountBalanceAsync(int accountId, decimal newBalance)
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();

            var acc = await conn.FindAsync<Account>(accountId);
            if (acc == null) return;
            acc.Balance = newBalance;
            await conn.UpdateAsync(acc);
        }

        public async Task<decimal> GetTotalAssetsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();
            var all = await conn.Table<Account>().ToListAsync();
            return all.Sum(a => a.Balance);
        }

        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();
            return await conn.FindAsync<Account>(accountId);
        }

        // -------------------- Trend --------------------

        public async Task<Dictionary<string, decimal>> GetLast4MonthsTotalAssetsAsync()
        {
            var today = DateTime.Today;
            var m1 = new DateTime(today.Year, today.Month, 1);
            var eoms = Enumerable.Range(-3, 4)
                                 .Select(i =>
                                 {
                                     var t = m1.AddMonths(i);
                                     return new DateTime(t.Year, t.Month,
                                         DateTime.DaysInMonth(t.Year, t.Month), 23, 59, 59, DateTimeKind.Local);
                                 })
                                 .ToArray();

            var totalNow = await GetTotalAssetsAsync();
            var startMonth = new DateTime(eoms.First().Year, eoms.First().Month, 1);
            var recs = await LoadRecordsFromBooksAsync(startMonth);

            var dict = new Dictionary<string, decimal>();
            foreach (var eom in eoms)
            {
                decimal deltaAfter = 0m;
                foreach (var r in recs)
                    if (r.ts > eom) deltaAfter += SignedFactor(r.type) * r.amount;

                dict[eom.ToString("yyyy-MM")] = totalNow - deltaAfter;
            }
            return dict;
        }

        // -------------------- Aggregation from books --------------------

        private async Task<Dictionary<int, decimal>> SumByAccountIdForBookAsync(string tableName)
        {
            var conn = await ConnAsync();
            var q = BookRepository.QuoteIdent(tableName);

            var rows = await conn.QueryAsync<(int AccountId, string Type, decimal Amount)>(
                $@"SELECT IFNULL(AccountId, 0) as AccountId,
                          IFNULL(Type, '')      as Type,
                          IFNULL(Amount, 0)     as Amount
                   FROM {q};");

            var map = new Dictionary<int, decimal>();
            foreach (var r in rows)
            {
                if (r.AccountId <= 0) continue;
                var sign = r.Type == "收入" ? 1m : (r.Type == "支出" ? -1m : 0m);
                var delta = sign * r.Amount;
                if (!map.ContainsKey(r.AccountId)) map[r.AccountId] = 0m;
                map[r.AccountId] += delta;
            }
            return map;
        }

        public async Task<List<(int AccountId, decimal Balance)>> GetAccountsWithBalancesFromBooksAsync(int? bookId = null)
        {
            var result = new Dictionary<int, decimal>();

            var books = (bookId.HasValue)
                ? new List<Book> { await _books.GetBookByIdAsync(bookId.Value) ?? throw new InvalidOperationException("Book not found.") }
                : await _books.ListBooksAsync();

            if (books.Count == 0)
            {
                await _books.ImportExistingPhysicalBooksIfAnyAsync();
                books = await _books.ListBooksAsync();
            }

            foreach (var b in books)
            {
                var per = await SumByAccountIdForBookAsync(b.TableName);
                foreach (var kv in per)
                {
                    if (!result.ContainsKey(kv.Key)) result[kv.Key] = 0m;
                    result[kv.Key] += kv.Value;
                }
            }

            return result.Select(kv => (kv.Key, kv.Value)).ToList();
        }

        public async Task SyncAccountsFromBooksAsync(int? bookId = null)
        {
            await EnsureDatabaseInitializedAsync();

            var conn = await ConnAsync();
            var pairs = await GetAccountsWithBalancesFromBooksAsync(bookId);

            foreach (var (accountId, balance) in pairs)
            {
                var existing = await conn.FindAsync<Account>(accountId);
                if (existing == null)
                {
                    try
                    {
                        await conn.InsertAsync(new Account
                        {
                            Id = accountId,
                            Name = $"Account_{accountId}",
                            Balance = balance,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    catch (SQLiteException)
                    {
                        // if a concurrent insert happened, just continue
                    }
                    existing = await conn.FindAsync<Account>(accountId);
                }
                if (existing != null)
                {
                    existing.Balance = balance;
                    await conn.UpdateAsync(existing);
                }
            }
        }

        // -------------------- Records helpers --------------------

        public async Task<List<Record>> ListRecordsForAccountAcrossBooksAsync(
            int accountId, DateTime monthStart, DateTime monthEndInclusive)
        {
            var books = await _books.ListBooksAsync();
            if (books.Count == 0)
            {
                await _books.ImportExistingPhysicalBooksIfAnyAsync();
                books = await _books.ListBooksAsync();
            }

            var conn = await ConnAsync();
            var result = new List<Record>();
            foreach (var b in books)
            {
                var rows = await conn.QueryAsync<Record>(
                    $@"SELECT Id, Type, Amount, Category, Note, Timestamp, AccountId
                       FROM {BookRepository.QuoteIdent(b.TableName)}
                       WHERE AccountId = ? AND Timestamp >= ? AND Timestamp <= ?
                       ORDER BY Timestamp DESC;",
                    accountId, monthStart, monthEndInclusive);
                result.AddRange(rows);
            }
            return result.OrderByDescending(r => r.Timestamp).ToList();
        }

        private async Task<List<(DateTime ts, string type, decimal amount)>> LoadRecordsFromBooksAsync(DateTime startMonth)
        {
            var books = await _books.ListBooksAsync();
            if (books.Count == 0)
            {
                await _books.ImportExistingPhysicalBooksIfAnyAsync();
                books = await _books.ListBooksAsync();
            }

            var conn = await ConnAsync();
            var list = new List<(DateTime ts, string type, decimal amount)>();

            foreach (var b in books)
            {
                var rows = await conn.QueryAsync<(string Timestamp, string Type, decimal Amount)>(
                    $@"SELECT Timestamp, IFNULL(Type,''), IFNULL(Amount,0)
                       FROM {BookRepository.QuoteIdent(b.TableName)};");

                foreach (var r in rows)
                {
                    if (!DateTime.TryParse(r.Timestamp, out var t)) continue;
                    var local = AsLocal(t);
                    if (local >= startMonth)
                        list.Add((local, r.Type, r.Amount));
                }
            }
            return list;
        }

        private static int SignedFactor(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return 0;
            var s = type.Trim();
            if (s.Equals("收入", StringComparison.OrdinalIgnoreCase)) return +1;
            if (s.Equals("支出", StringComparison.OrdinalIgnoreCase)) return -1;
            if (s.Equals("转账", StringComparison.OrdinalIgnoreCase)) return 0;
            if (s.Equals("income", StringComparison.OrdinalIgnoreCase)) return +1;
            if (s.Equals("expense", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("expensee", StringComparison.OrdinalIgnoreCase)) return -1;
            if (s.Equals("transfer", StringComparison.OrdinalIgnoreCase)) return 0;
            return 0;
        }

        private static DateTime AsLocal(DateTime dt) =>
            dt.Kind switch
            {
                DateTimeKind.Utc => dt.ToLocalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Local),
                _ => dt
            };
    }
}
