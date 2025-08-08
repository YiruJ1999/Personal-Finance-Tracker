using PersonalFinanceTracker.Models;
using SQLite;
using System.IO;

namespace PersonalFinanceTracker.Services
{
    public class DatabaseService
    {
        public SQLiteAsyncConnection Database { get; private set; }

        private static readonly string dbPath = Path.Combine(FileSystem.AppDataDirectory, "finance.db3");

        public async Task InitAsync()
        {
            if (Database != null)
                return;

            Database = new SQLiteAsyncConnection(dbPath);

            await Database.CreateTableAsync<Record>();
            await Database.CreateTableAsync<PersonalInfo>();
            await Database.CreateTableAsync<Account>();
        }

    }
}
