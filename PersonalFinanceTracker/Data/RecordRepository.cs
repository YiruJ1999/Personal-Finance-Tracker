using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.Data
{
    public class RecordRepository
    {
        private readonly DatabaseService _database;

        public RecordRepository(DatabaseService database)
        {
            _database = database;
        }

        public Task<List<Record>> ListAsync()
        {
            return _database.Database.Table<Record>().ToListAsync();
        }

        public Task<Record> GetByIdAsync(int id)
        {
            return _database.Database.FindAsync<Record>(id);
        }

        public Task SaveAsync(Record record)
        {
            if (record.ID == 0)
                return _database.Database.InsertAsync(record);
            else
                return _database.Database.UpdateAsync(record);
        }

        public Task DeleteAsync(Record record)
        {
            return _database.Database.DeleteAsync(record);
        }

        public Task DeleteAllAsync()
        {
            return _database.Database.DeleteAllAsync<Record>();
        }

    }
}
