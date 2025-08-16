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

        // Get a live connection; auto-init if needed.
        private async Task<SQLiteAsyncConnection> ConnAsync()
        {
            if (_dbService.Database == null)
                await _dbService.InitAsync();
            return _dbService.Database!;
        }

        public async Task EnsureDatabaseInitializedAsync()
        {
            var conn = await ConnAsync();
            await conn.CreateTableAsync<Account>();
        }

        // ---------------------------
        // Basic account operations
        // ---------------------------

        private async Task<Account?> GetAccountByNameAsync(string name)
        {
            var conn = await ConnAsync();
            const string sql = @"SELECT * FROM Account WHERE Name = ? COLLATE NOCASE LIMIT 1;";
            var rows = await conn.QueryAsync<Account>(sql, name);
            return rows.FirstOrDefault();
        }

        public async Task<List<Account>> ListAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();
            return await conn.Table<Account>().OrderBy(a => a.Name).ToListAsync();
        }

        public async Task<Account> AddAccountAsync(string name)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Account name is required.", nameof(name));

            var conn = await ConnAsync();
            var trimmed = name.Trim();
            var exists = await GetAccountByNameAsync(trimmed);
            if (exists != null) return exists;

            var acc = new Account { Name = trimmed, Balance = 0m, CreatedAt = DateTime.UtcNow };
            await conn.InsertAsync(acc); // acc.Id populated
            return acc;
        }

        public async Task<Account> AddAccountAsync(string name, decimal openingBalance)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Account name is required.", nameof(name));

            var conn = await ConnAsync();
            var trimmed = name.Trim();
            var exists = await GetAccountByNameAsync(trimmed);
            if (exists == null)
            {
                var acc = new Account { Name = trimmed, Balance = openingBalance, CreatedAt = DateTime.UtcNow };
                await conn.InsertAsync(acc);
                return acc;
            }
            else
            {
                exists.Balance = openingBalance;
                await conn.UpdateAsync(exists);
                return exists;
            }
        }

        public async Task DeleteAccountAsync(int accountId, bool alsoDeleteRecords)
        {
            await EnsureDatabaseInitializedAsync();
            var conn = await ConnAsync();

            await conn.ExecuteAsync(@"DELETE FROM Account WHERE Id = ?;", accountId);

            if (alsoDeleteRecords)
            {
                var books = await _books.ListBooksAsync();
                foreach (var b in books)
                {
                    await conn.ExecuteAsync($@"DELETE FROM {BookRepository.QuoteIdent(b.TableName)} WHERE AccountId = ?;", accountId);
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
            var list = await conn.Table<Account>().ToListAsync();
            return list.Sum(a => a.Balance);
        }

        public async Task<Dictionary<string, decimal>> GetLast4MonthsTotalAssetsAsync()
        {
            var today = DateTime.Today;
            var eomList = new List<DateTime>();
            for (int i = 3; i >= 0; i--)
            {
                var target = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                var eom = new DateTime(target.Year, target.Month,
                    DateTime.DaysInMonth(target.Year, target.Month), 23, 59, 59, DateTimeKind.Local);
                eomList.Add(eom);
            }

            var totalNow = await GetTotalAssetsAsync();

            var books = await _books.ListBooksAsync();
            if (books.Count == 0)
            {
                await _books.ImportExistingPhysicalBooksIfAnyAsync();
                books = await _books.ListBooksAsync();
            }

            var conn = await ConnAsync();
            var all = new List<(DateTime ts, string type, decimal amount)>();
            foreach (var b in books)
            {
                var rows = await conn.QueryAsync<(string Timestamp, string Type, decimal Amount)>(
                    $@"SELECT Timestamp, IFNULL(Type,''), IFNULL(Amount,0) FROM {BookRepository.QuoteIdent(b.TableName)};");
                foreach (var r in rows)
                {
                    if (DateTime.TryParse(r.Timestamp, out var t))
                        all.Add((t, r.Type, r.Amount));
                }
            }

            decimal Sign(string t) => t == "收入" ? 1m : (t == "支出" ? -1m : 0m);

            var result = new Dictionary<string, decimal>();
            foreach (var eom in eomList)
            {
                var deltaAfter = all.Where(x => x.ts > eom).Sum(x => Sign(x.type) * x.amount);
                result[eom.ToString("yyyy-MM")] = totalNow - deltaAfter;
            }
            return result;
        }

        // ---------------------------
        // Aggregation by AccountId
        // ---------------------------

        private async Task<Dictionary<int, decimal>> SumByAccountIdForBookAsync(string tableName)
        {
            var conn = await ConnAsync();
            var q = BookRepository.QuoteIdent(tableName);

            var rows = await conn.QueryAsync<(int AccountId, string Type, decimal Amount)>(
                $@"SELECT IFNULL(AccountId, 0) as AccountId, IFNULL(Type, '') as Type, IFNULL(Amount, 0) as Amount
                   FROM {q};");

            var map = new Dictionary<int, decimal>();
            foreach (var r in rows)
            {
                if (r.AccountId <= 0) continue; // skip rows not linked to an account
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
                var perBook = await SumByAccountIdForBookAsync(b.TableName);
                foreach (var kv in perBook)
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
                // Try find by Id
                var existing = await conn.FindAsync<Account>(accountId);
                if (existing == null)
                {
                    // Create a placeholder row with explicit Id (in case records reference this Id)
                    // Use INSERT OR IGNORE to avoid crashing if Id somehow appears concurrently.
                    var affected = await conn.ExecuteAsync(
                        @"INSERT OR IGNORE INTO Account (Id, Name, Balance, CreatedAt) VALUES (?, ?, ?, ?);",
                        accountId, $"Account_{accountId}", balance, DateTime.UtcNow);

                    if (affected == 0)
                    {
                        // Row exists but FindAsync failed? Try load again.
                        existing = await conn.FindAsync<Account>(accountId);
                        if (existing == null) continue;
                        existing.Balance = balance;
                        await conn.UpdateAsync(existing);
                    }
                }
                else
                {
                    existing.Balance = balance;
                    await conn.UpdateAsync(existing);
                }
            }
        }

        // ---------------------------
        // Cross-book records by account
        // ---------------------------

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
            var results = new List<Record>();
            foreach (var b in books)
            {
                var rows = await conn.QueryAsync<Record>(
                    $@"SELECT ID, Type, Amount, Category, Note, Timestamp, Account, AccountId
                       FROM {BookRepository.QuoteIdent(b.TableName)}
                       WHERE AccountId = ? AND Timestamp >= ? AND Timestamp <= ?
                       ORDER BY Timestamp DESC;",
                    accountId, monthStart, monthEndInclusive);
                results.AddRange(rows);
            }
            return results.OrderByDescending(r => r.Timestamp).ToList();
        }
    }
}
