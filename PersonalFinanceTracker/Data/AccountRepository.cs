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
        private readonly SQLiteAsyncConnection _db;
        private readonly BookRepository _books;

        public AccountRepository(DatabaseService database, BookRepository books)
        {
            _db = database.Database;
            _books = books;
        }

        public async Task EnsureDatabaseInitializedAsync()
        {
            if (_db == null) throw new InvalidOperationException("Database connection is null.");
            await _db.CreateTableAsync<Account>();
        }

        public async Task<List<Account>> ListAsync()
        {
            await EnsureDatabaseInitializedAsync();
            return await _db.Table<Account>().OrderBy(a => a.Name).ToListAsync();
        }

        // ---------- Basic operations by Id ----------
        public async Task DeleteAccountAsync(int accountId, bool alsoDeleteRecords)
        {
            await EnsureDatabaseInitializedAsync();

            await _db.ExecuteAsync(@"DELETE FROM Account WHERE Id = ?;", accountId);

            if (alsoDeleteRecords)
            {
                var books = await _books.ListBooksAsync();
                foreach (var b in books)
                {
                    await _db.ExecuteAsync($@"DELETE FROM {BookRepository.QuoteIdent(b.TableName)} WHERE AccountId = ?;", accountId);
                }
            }
        }

        public async Task UpdateAccountBalanceAsync(int accountId, decimal newBalance)
        {
            await EnsureDatabaseInitializedAsync();
            var acc = await _db.FindAsync<Account>(accountId);
            if (acc == null) return;

            acc.Balance = newBalance;
            await _db.UpdateAsync(acc);
        }

        // ---------- Aggregation (by AccountId) ----------

        /// <summary>
        /// Sum per AccountId across one specific book.
        /// </summary>
        private async Task<Dictionary<int, decimal>> SumByAccountIdForBookAsync(string tableName)
        {
            var q = BookRepository.QuoteIdent(tableName);
            var rows = await _db.QueryAsync<(int AccountId, string Type, decimal Amount)>($@"
                SELECT IFNULL(AccountId, 0) as AccountId, IFNULL(Type, '') as Type, IFNULL(Amount, 0) as Amount
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

        /// <summary>
        /// Aggregate balances per AccountId across all or one book.
        /// </summary>
        public async Task<List<(int AccountId, decimal Balance)>> GetAccountsWithBalancesFromBooksAsync(int? bookId = null)
        {
            var result = new Dictionary<int, decimal>();

            var books = (bookId.HasValue)
                ? new List<Book> { await _books.GetBookByIdAsync(bookId.Value) ?? throw new InvalidOperationException("Book not found.") }
                : await _books.ListBooksAsync();

            if (books.Count == 0)
            {
                // Self-heal: if Books empty, try import existing tables
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

        /// <summary>
        /// Upsert Account table's Balance by AccountId using aggregated values.
        /// Note: Account rows must exist (migration guarantees creation).
        /// </summary>
        public async Task SyncAccountsFromBooksAsync(int? bookId = null)
        {
            await EnsureDatabaseInitializedAsync();

            var pairs = await GetAccountsWithBalancesFromBooksAsync(bookId);

            foreach (var (accountId, balance) in pairs)
            {
                var existing = await _db.FindAsync<Account>(accountId);
                if (existing == null)
                {
                    // Safety: create a placeholder if somehow missing
                    await _db.InsertAsync(new Account
                    {
                        Id = accountId,
                        Name = $"Account_{accountId}",
                        Balance = balance,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.Balance = balance;
                    await _db.UpdateAsync(existing);
                }
            }
        }

        // ---------- UI helpers ----------

        public async Task<List<Account>> GetAccountsWithBalancesAsync()
        {
            await EnsureDatabaseInitializedAsync();
            return await _db.Table<Account>().OrderBy(a => a.Name).ToListAsync();
        }

        public async Task<decimal> GetTotalAssetsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var list = await _db.Table<Account>().ToListAsync();
            return list.Sum(a => a.Balance);
        }

        public async Task<Dictionary<string, decimal>> GetLast4MonthsTotalAssetsAsync()
        {
            // Build last-3 to current month EOM (ascending)
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

            var all = new List<(DateTime ts, string type, decimal amount)>();
            foreach (var b in books)
            {
                var rows = await _db.QueryAsync<(string Timestamp, string Type, decimal Amount)>(
                    $@"SELECT Timestamp, IFNULL(Type,''), IFNULL(Amount,0) FROM {BookRepository.QuoteIdent(b.TableName)};");
                foreach (var r in rows)
                {
                    DateTime t;
                    if (!DateTime.TryParse(r.Timestamp, out t)) continue;
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

        public async Task<List<Record>> ListRecordsForAccountAcrossBooksAsync(
            int accountId, DateTime monthStart, DateTime monthEndInclusive)
        {
            var books = await _books.ListBooksAsync();
            if (books.Count == 0)
            {
                await _books.ImportExistingPhysicalBooksIfAnyAsync();
                books = await _books.ListBooksAsync();
            }

            var results = new List<Record>();
            foreach (var b in books)
            {
                var rows = await _db.QueryAsync<Record>(
                    $@"SELECT Id, Type, Amount, Category, Note, Timestamp, Account, AccountId
                       FROM {BookRepository.QuoteIdent(b.TableName)}
                       WHERE AccountId = ? AND Timestamp >= ? AND Timestamp <= ?
                       ORDER BY Timestamp DESC;",
                    accountId, monthStart, monthEndInclusive);
                results.AddRange(rows);
            }
            return results.OrderByDescending(r => r.Timestamp).ToList();
        }


        private async Task<Account?> GetAccountByNameAsync(string name)
        {
            const string sql = @"SELECT * FROM Account WHERE Name = ? COLLATE NOCASE LIMIT 1;";
            var rows = await _db.QueryAsync<Account>(sql, name);
            return rows.FirstOrDefault();
        }

        // create or return existing (case-insensitive) 
        public async Task<Account> AddAccountAsync(string name)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Account name is required.", nameof(name));

            var trimmed = name.Trim();
            var exists = await GetAccountByNameAsync(trimmed);
            if (exists != null) return exists;

            var acc = new Account { Name = trimmed, Balance = 0m, CreatedAt = DateTime.UtcNow };
            await _db.InsertAsync(acc);            // acc.Id will be populated
            return acc;
        }

        //create or upsert with opening balance 
        public async Task<Account> AddAccountAsync(string name, decimal openingBalance)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Account name is required.", nameof(name));

            var trimmed = name.Trim();
            var exists = await GetAccountByNameAsync(trimmed);
            if (exists == null)
            {
                var acc = new Account { Name = trimmed, Balance = openingBalance, CreatedAt = DateTime.UtcNow };
                await _db.InsertAsync(acc);        // acc.Id populated
                return acc;
            }
            else
            {
                exists.Balance = openingBalance;
                await _db.UpdateAsync(exists);
                return exists;
            }
        }

    }
}
