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


        /// <summary>
        /// Completely delete the local SQLite database file.
        /// </summary>
        public Task ClearDatabaseAsync()
        {
            if (File.Exists(dbPath))
            {
                Database = null; // release connection so SQLite file can be deleted
                File.Delete(dbPath);
            }
            return Task.CompletedTask;
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

    class _ScalarRow<TScalar> { public TScalar Value { get; set; } = default!; }
    }

}



