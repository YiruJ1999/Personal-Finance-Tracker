```csharp
using SQLite;
using PersonalFinanceApp.Models;

namespace PersonalFinanceApp.Services;

public class DatabaseService
{
    readonly SQLiteAsyncConnection _db;

    public DatabaseService(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Record>().Wait();
    }

    public Task<int> AddRecordAsync(Record record) => _db.InsertAsync(record);

    public Task<List<Record>> GetRecordsAsync(DateTime? start = null, DateTime? end = null)
    {
        if (start != null && end != null)
            return _db.Table<Record>().Where(r => r.Timestamp >= start && r.Timestamp <= end).ToListAsync();
        return _db.Table<Record>().ToListAsync();
    }
}
```
