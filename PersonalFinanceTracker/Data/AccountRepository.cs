using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.Data
{
    public class AccountRepository
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
}