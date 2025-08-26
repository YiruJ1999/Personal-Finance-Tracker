using SQLite;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

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

        public async Task<SQLiteAsyncConnection> GetConnectionAsync()
        {
            if (Database == null)
                await InitAsync();
            return Database!;
        }

        // Optional convenience wrappers (keep them generic).
        public Task<int> ExecuteAsync(string sql, params object[] args)
            => Database.ExecuteAsync(sql, args);

        public Task<T> ExecuteScalarAsync<T>(string sql, params object[] args)
            => Database.ExecuteScalarAsync<T>(sql, args);

        public Task<System.Collections.Generic.List<T>> QueryAsync<T>(string sql, params object[] args)
            where T : new()
            => Database.QueryAsync<T>(sql, args);


        /// <summary>
        /// Clear all user tables from the existing SQLite file WITHOUT deleting the file itself.
        /// Drops every table except SQLite internal ones (sqlite_%), then recreates base tables
        /// needed by the app to continue running.
        /// </summary>
        public async Task ClearDatabaseAsync()
        {
            // Ensure we have a connection
            if (Database == null)
                await InitAsync();

            // 1) Query all user table names (exclude internal sqlite_% tables)
            var tableNames = await QueryScalarsAsync<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';");

            // 2) Temporarily disable foreign key checks to avoid drop-order issues
            await Database.ExecuteAsync("PRAGMA foreign_keys=OFF;");

            try
            {
                // 3) Drop each user table
                foreach (var name in tableNames)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    await Database.ExecuteAsync($"DROP TABLE IF EXISTS {QuoteIdent(name)};");
                }
            }
            finally
            {
                // 4) Re-enable foreign key checks
                await Database.ExecuteAsync("PRAGMA foreign_keys=ON;");
            }

        }

        public Task<int> InsertAsync<T>(T obj) where T : new()
            => Database.InsertAsync(obj);

        public Task<int> UpdateAsync<T>(T obj) where T : new()
            => Database.UpdateAsync(obj);

        public Task<int> DeleteAsync<T>(T obj) where T : new()
            => Database.DeleteAsync(obj);

        public Task<T> FindAsync<T>(object pk) where T : new()
            => Database.FindAsync<T>(pk);

        // Some sqlite-net versions don't have QueryScalarsAsync; if you used it elsewhere,
        // you can emulate it like this:
        public async Task<List<T>> QueryScalarsAsync<T>(string sql, params object[] args)
        {
            // comment: wrap scalar into a DTO to map
            var rows = await Database.QueryAsync<_ScalarRow<T>>(sql, args);
            return rows.Select(r => r.Value).ToList();
        }

        private static string QuoteIdent(string ident)
        {
            // Safely quote SQLite identifiers with double quotes and escape inner quotes.
            return "\"" + (ident ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }

        class _ScalarRow<TScalar> { public TScalar Value { get; set; } = default!; }
    }

}



