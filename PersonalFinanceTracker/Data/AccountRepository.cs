using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SQLite;
using PersonalFinanceTracker.Models;    
using PersonalFinanceTracker.Services;  

namespace PersonalFinanceTracker.Data
{
    public class AccountRepository
    {
        private readonly SQLiteAsyncConnection _db;

        public AccountRepository(DatabaseService database)
        {
            _db = database.Database;
        }


        public async Task EnsureDatabaseInitializedAsync()
        {
            if (_db == null)
                throw new InvalidOperationException("Database connection is null.");
            await _db.CreateTableAsync<Account>();
        }

        // List all accounts (raw table).
        public async Task<List<Account>> ListAsync()
        {
            await EnsureDatabaseInitializedAsync();
            return await _db.Table<Account>().ToListAsync();
        }

        // Normalize account name: trim, empty → "默认"
        private static string NormalizeAccountName(string? name)
        {
            var n = name?.Trim();
            // 空或空白账户名 → “默认”
            return string.IsNullOrWhiteSpace(n) ? "默认" : n;
        }



        // Add an account with zero balance if not exists.
        public async Task AddAccountAsync(string name)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name)) return;

            var exists = await _db.Table<Account>().Where(a => a.Name == name).FirstOrDefaultAsync();
            if (exists == null)
            {
                await _db.InsertAsync(new Account
                {
                    Name = name.Trim(),
                    Balance = 0m,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Overload: add an account with an opening balance.
        public async Task AddAccountAsync(string name, decimal openingBalance)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name)) return;

            var trimmed = name.Trim();
            var exists = await _db.Table<Account>().Where(a => a.Name == trimmed).FirstOrDefaultAsync();
            if (exists == null)
            {
                await _db.InsertAsync(new Account
                {
                    Name = trimmed,
                    Balance = openingBalance,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                exists.Balance = openingBalance;
                await _db.UpdateAsync(exists);
            }
        }

        // Delete an account. If alsoDeleteRecords == true, remove all records in all book_* tables for this account.
        public async Task DeleteAccountAsync(string name, bool alsoDeleteRecords)
        {
            await EnsureDatabaseInitializedAsync();
            if (string.IsNullOrWhiteSpace(name)) return;

            var acc = await _db.Table<Account>().Where(a => a.Name == name).FirstOrDefaultAsync();
            if (acc != null)
                await _db.DeleteAsync(acc);

            if (alsoDeleteRecords)
            {
                var tables = await GetBookTableNamesAsync();
                foreach (var t in tables)
                {
                    var tn = SanitizeTableName(t);
                    // Raw SQL delete by account name
                    await _db.ExecuteAsync($"DELETE FROM {tn} WHERE Account = ?", name);
                }
            }
        }

        // Directly set account balance (NO record written).
        // For record-driven adjustments, write a Record (Adjustment) in a specific book instead.
        public async Task UpdateAccountBalanceAsync(string name, decimal newBalance)
        {
            await EnsureDatabaseInitializedAsync();
            var acc = await _db.Table<Account>().Where(a => a.Name == name).FirstOrDefaultAsync();
            if (acc == null) return;

            acc.Balance = newBalance;
            await _db.UpdateAsync(acc);
        }

        // ---------------------------
        // Multi-ledger aggregation
        // ---------------------------

        // Get all ledger table names: book_*
        public async Task<List<string>> GetBookTableNamesAsync()
        {
            var rows = await _db.QueryScalarsAsync<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'book_%';");
            return rows?.ToList() ?? new List<string>();
        }

        private static string SanitizeTableName(string table)
        {
            return Regex.Replace(table ?? string.Empty, @"[^a-zA-Z0-9_]+", "");
        }

        /// <summary>
        /// Aggregate account balances scanning one or all book tables.
        /// Income => +Amount; Expense => -Amount; (extend types as needed)
        /// </summary>
        public async Task<List<(string Account, decimal Balance)>> GetAccountsWithBalancesFromBooksAsync(string? book = null)
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            List<string> tables;
            if (!string.IsNullOrWhiteSpace(book))
                tables = new List<string> { $"book_{SanitizeTableName(book)}" };
            else
                tables = await GetBookTableNamesAsync();

            foreach (var t in tables)
            {
                var tn = SanitizeTableName(t);
                var rows = await _db.QueryAsync<Record>($"SELECT * FROM {tn}");

                foreach (var r in rows)
                {
                    var key = NormalizeAccountName(r.Account);
                    var sign = r.Type == "收入" ? 1m :
                               r.Type == "支出" ? -1m : 0m; // extend for OpeningBalance/Adjustment if you have
                    var delta = sign * r.Amount;

                    if (!result.ContainsKey(key))
                        result[key] = 0m;

                    result[key] += delta;
                }
            }

            return result.Select(kv => (kv.Key, kv.Value)).ToList();
        }

        // Upsert Account table from aggregated balances (all books or a single book).
        public async Task SyncAccountsFromBooksAsync(string? book = null)
        {
            await EnsureDatabaseInitializedAsync();

            var pairs = await GetAccountsWithBalancesFromBooksAsync(book);

            foreach (var (name, balance) in pairs)
            {
                var normalized = NormalizeAccountName(name);
                var existing = await _db.Table<Account>()
                    .Where(a => a.Name.ToLower() == normalized.ToLower())
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    await _db.InsertAsync(new Account { Name = name, Balance = balance, CreatedAt = DateTime.UtcNow });
                }
                else
                {
                    existing.Balance = balance;
                    await _db.UpdateAsync(existing);
                }
            }
        }

        // ---------------------------
        // Compatibility shims for AccountPageModel
        // ---------------------------

        // Compatibility: previously "from Records". In multi-ledger we scan all book_* tables.
        public async Task SyncAccountsFromRecordsAsync()
        {
            await SyncAccountsFromBooksAsync(null);
        }


        // Return accounts with balances for UI. Here we simply read Account table after last sync.
        public async Task<List<Account>> GetAccountsWithBalancesAsync()
        {
            await EnsureDatabaseInitializedAsync();
            return await _db.Table<Account>().OrderBy(a => a.Name).ToListAsync();
        }
        
        // Sum of all account balances (after sync).

        public async Task<decimal> GetTotalAssetsAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var list = await _db.Table<Account>().ToListAsync();
            return list.Sum(a => a.Balance);
        }


        // Last 4 months end-of-month total assets trend (YYYY-MM -> total).
        // Implementation: for each month end, aggregate all records up to that date across all books.
        public async Task<Dictionary<string, decimal>> GetLast4MonthsTotalAssetsAsync()
        {
            // 1) Build the 4 month-ends, ascending.
            var today = DateTime.Today;
            var eomList = new List<DateTime>();
            for (int i = 3; i >= 0; i--)
            {
                var firstDayThisMonth = new DateTime(today.Year, today.Month, 1);
                var dt = firstDayThisMonth.AddMonths(-i + 1).AddDays(-1); // last day of target month
                var eom = new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month), 23, 59, 59, DateTimeKind.Local);
                eomList.Add(eom);
            }

            // 2) Current total assets from Account table (includes opening balances etc.)
            var totalNow = await GetTotalAssetsAsync();

            // 3) Load ALL records across books once.
            var tables = await GetBookTableNamesAsync();
            var allRecords = new List<Record>();
            foreach (var t in tables)
            {
                var tn = SanitizeTableName(t);
                var rows = await _db.QueryAsync<Record>($"SELECT * FROM {tn}");
                allRecords.AddRange(rows);
            }

            // Normalize timestamps to Local.
            foreach (var r in allRecords)
            {
                if (r.Timestamp.Kind == DateTimeKind.Unspecified)
                    r.Timestamp = DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Local);
            }

            // Helper to map record type to sign.
            decimal Sign(Record r) => r.Type == "收入" ? 1m : (r.Type == "支出" ? -1m : 0m);

            // 4) Reconstruct end-of-month total assets:
            //    totalAtEom = totalNow - sum(net flows AFTER that EOM)
            var result = new Dictionary<string, decimal>();
            foreach (var eom in eomList)
            {
                var deltaAfter = allRecords
                    .Where(x => x.Timestamp > eom)
                    .Sum(x => Sign(x) * x.Amount);

                var totalAtEom = totalNow - deltaAfter;
                result[eom.ToString("yyyy-MM")] = totalAtEom;
            }

            return result;
        }

        public async Task<List<Record>> ListRecordsForAccountAcrossBooksAsync(
            string accountNameRaw, DateTime monthStart, DateTime monthEndInclusive)
        {
            var target = NormalizeAccountName(accountNameRaw);

            var results = new List<Record>();
            var tables = await GetBookTableNamesAsync(); // e.g., ["book_default", "book_xxx"]

            foreach (var t in tables)
            {
                var tn = SanitizeTableName(t);
                var rows = await _db.QueryAsync<Record>($"SELECT * FROM {tn}");
                results.AddRange(rows);
            }

            // Normalize timestamps and account names, then filter in-memory
            foreach (var r in results)
            {
                if (r.Timestamp.Kind == DateTimeKind.Unspecified)
                    r.Timestamp = DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Local);
                else if (r.Timestamp.Kind == DateTimeKind.Utc)
                    r.Timestamp = r.Timestamp.ToLocalTime();
            }

            return results
                .Where(r => NormalizeAccountName(r.Account).Equals(target, StringComparison.OrdinalIgnoreCase)
                         && r.Timestamp >= monthStart && r.Timestamp <= monthEndInclusive)
                .OrderByDescending(r => r.Timestamp)
                .ToList();
        }

    }
}
