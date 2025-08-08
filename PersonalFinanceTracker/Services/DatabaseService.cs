using SQLite;
using System.IO;
using System.Threading.Tasks;

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

            // Keep only cross-cutting init here if really needed (e.g., app-wide metadata tables).
            // Do NOT put Record-specific logic here.
            await Database.CreateTableAsync<Models.Record>();
            await Database.CreateTableAsync<Models.PersonalInfo>();
            await Database.CreateTableAsync<Models.Account>();
        }

        // Optional convenience wrappers (keep them generic).
        public Task<int> ExecuteAsync(string sql, params object[] args)
            => Database.ExecuteAsync(sql, args);

        public Task<T> ExecuteScalarAsync<T>(string sql, params object[] args)
            => Database.ExecuteScalarAsync<T>(sql, args);

        public Task<System.Collections.Generic.List<T>> QueryAsync<T>(string sql, params object[] args)
            where T : new()
            => Database.QueryAsync<T>(sql, args);
    }
}
