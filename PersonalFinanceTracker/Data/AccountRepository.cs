using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using System.Globalization;

namespace PersonalFinanceTracker.Data;

public partial class AccountRepository
{
    private readonly DatabaseService _database;
    public AccountRepository(DatabaseService database)
    {
        _database = database;
    }
    public async Task EnsureDatabaseInitializedAsync()
    {
        await _database.InitAsync();
    }
    // Get all accounts
    public Task<List<Account>> ListAsync()
    {
        return _database.Database.Table<Account>().ToListAsync();
    }
    // Save or update an account
    public Task SaveAsync(Account account)
    {
        if (account.id == 0)
            return _database.Database.InsertAsync(account);
        else
            return _database.Database.UpdateAsync(account);
    }
}



public class AccountBalanceDto
{
    // DTO for raw SQL projection
    public string Account { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class MonthlyTotalDto
{
    public string MonthKey { get; set; } = string.Empty; 
    public decimal NetDelta { get; set; } // Income - Expense in that month
}

public partial class AccountRepository
{
    // 1) Ensure all distinct accounts from Records exist in Accounts table, and set balance from history
    public async Task SyncAccountsFromRecordsAsync()
    {
        await _database.InitAsync(); // ensure DB init

        // Get distinct account names from records
        var records = await _database.Database.Table<Record>()
            .Where(r => !string.IsNullOrEmpty(r.Account))
            .ToListAsync(); 

        var distinctNames = records
            .Select(r => r.Account)            
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Create accounts if missing
        var existing = await _database.Database.Table<Account>().ToListAsync();
        var existingSet = existing.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in distinctNames)
        {
            if (!existingSet.Contains(name))
            {
                await _database.Database.InsertAsync(new Account
                {
                    Name = name,
                    Balance = 0m, // will be updated right below
                    Type = "",    // optional
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Recompute balance for each account from Records (Income - Expense)

        // AccountRepository.SyncAccountsFromRecordsAsync()
        var sql = @"
            SELECT Account as Account,
                   SUM(CASE 
                         WHEN Type IN ('Income','收入','OpeningBalance','Adjustment-Increase') THEN Amount
                         ELSE -Amount
                       END) as Balance
            FROM Records
            WHERE Account IS NOT NULL AND Account <> ''
            GROUP BY Account";
        var aggregates = await _database.Database.QueryAsync<AccountBalanceDto>(sql);


        foreach (var agg in aggregates)
        {
            var acc = await _database.Database.Table<Account>()
                .Where(a => a.Name == agg.Account)
                .FirstOrDefaultAsync();

            if (acc != null)
            {
                acc.Balance = agg.Balance;
                await _database.Database.UpdateAsync(acc);
            }
        }
    }

    // 2) Total assets (sum of all account balances)
    public async Task<decimal> GetTotalAssetsAsync()
    {
        await _database.InitAsync();
        var accounts = await _database.Database.Table<Account>().ToListAsync();
        return accounts.Sum(a => a.Balance);
    }

    // 3) Get current balances of all accounts (for listing in UI)
    public async Task<List<Account>> GetAccountsWithBalancesAsync()
    {
        await _database.InitAsync();
        return await _database.Database.Table<Account>().OrderBy(a => a.Name).ToListAsync();
    }

    // 4) Update one account's balance (manual adjustment)
    public async Task UpdateAccountBalanceAsync(string accountName, decimal newBalance)
    {
        await _database.InitAsync();

        var sqlCurrent = @"
        SELECT 
            SUM(CASE 
                  WHEN Type IN ('Income','收入','OpeningBalance','Adjustment-Increase') THEN Amount
                  ELSE -Amount
                END)
        FROM Records
        WHERE Account = ?";
        var current = await _database.Database.ExecuteScalarAsync<decimal?>(sqlCurrent, accountName) ?? 0m;
        // calculate the difference
        var delta = newBalance - current;
        if (delta == 0m)
            return;

        var adj = new Record
        {
            Account = accountName,
            Type = delta >= 0 ? "Adjustment-Increase" : "Adjustment-Decrease",
            Amount = Math.Abs(delta),
            Category = "Adjustment",
            Note = "Manual balance adjustment",
            Timestamp = DateTime.UtcNow
        };
        await _database.Database.InsertAsync(adj);

        await SyncAccountsFromRecordsAsync();
    }


    // 5) Add a new account
    public async Task AddAccountAsync(string accountName, decimal openingBalance = 0m)
    {
        await _database.InitAsync();
        var exists = await _database.Database.Table<Account>().Where(a => a.Name == accountName).FirstOrDefaultAsync();
        if (exists != null) return;

        var acc = new Account
        {
            Name = accountName,
            Balance = openingBalance,
            Type = "",
            CreatedAt = DateTime.UtcNow
        };
        await _database.Database.InsertAsync(acc);

        if (openingBalance != 0m)
        {
            // Optional: write an opening balance record
            await _database.Database.InsertAsync(new Record
            {
                Account = accountName,
                Type = "OpeningBalance",
                Amount = openingBalance,
                Category = "Opening",
                Note = "Opening balance",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    // 6) Delete an account (optionally cascade delete its records)
    public async Task DeleteAccountAsync(string accountName, bool alsoDeleteRecords = false)
    {
        await _database.InitAsync();
        var acc = await _database.Database.Table<Account>().Where(a => a.Name == accountName).FirstOrDefaultAsync();
        if (acc != null)
        {
            await _database.Database.DeleteAsync(acc);
        }

        if (alsoDeleteRecords)
        {
            // delete ALL records of this account
            var sql = "DELETE FROM Records WHERE Account = ?";
            await _database.Database.ExecuteAsync(sql, accountName);
        }
    }

    // 7) Last 4 months total assets trend (month-end snapshot)

    public async Task<Dictionary<string, decimal>> GetLast4MonthsTotalAssetsAsync()
    {
        await _database.InitAsync();

        // Build the last 4 months (ascending)
        var months = Enumerable.Range(0, 4)
            .Select(i => DateTime.UtcNow.AddMonths(-i))
            .OrderBy(d => d)
            .Select(d => d.ToString("yyyy-MM"))
            .ToList();

        // Pull all records and group in C# to avoid SQLite strftime quirks
        var all = await _database.Database.Table<Record>().ToListAsync();

        // Month key: yyyy-MM; Net delta: + for income/opening/adjust+, - for expense/adjust-
        var monthlyDict = all
            .GroupBy(r => r.Timestamp.ToString("yyyy-MM"))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(r =>
                    (r.Type == "Income" ||
                     r.Type == "收入" ||
                     r.Type == "OpeningBalance" ||
                     r.Type == "Adjustment-Increase")
                    ? r.Amount
                    : -r.Amount
                )
            );

        // Build cumulative series across the 4 months window
        var trend = new Dictionary<string, decimal>();
        decimal cumulative = 0m;
        foreach (var m in months)
        {
            if (monthlyDict.TryGetValue(m, out var delta))
                cumulative += delta;

            trend[m] = cumulative;
        }

        return trend;
    }



}