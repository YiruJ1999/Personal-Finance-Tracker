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
        }

        // Save a record to the database
        public async Task SaveRecordAsync(Record record)
        {
            await InitAsync(); // 确保数据库已初始化
            await Database.InsertAsync(record);
        }

        // Get all records from the database
        public async Task<List<Record>> GetAllRecordsAsync()
        {
            await InitAsync();
            return await Database.Table<Record>().ToListAsync();
        }

        // Delete all records from the database
        public async Task DeleteAllRecordsAsync()
        {
            await InitAsync();
            await Database.DeleteAllAsync<Record>();
        }

        // Delete a record
        public async Task DeleteRecordAsync(Record record)
        {
            await InitAsync();
            await Database.DeleteAsync(record);
        }

    }
}
