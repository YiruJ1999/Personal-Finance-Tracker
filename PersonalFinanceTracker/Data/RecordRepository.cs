using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;

namespace PersonalFinanceTracker.Data
{
    public class RecordRepository
    {
        private readonly DatabaseService _database;

        public RecordRepository(DatabaseService database)
        {
            _database = database;
        }

        /// <summary>
        /// Build a safe physical table name for a given book.
        /// Only letters, digits and underscore are kept to avoid SQL injection on identifiers.
        /// Final form: Record_{Sanitized}
        /// </summary>
        private static string GetRecordTableName(string bookName)
        {
            string sanitized = Regex.Replace(bookName ?? string.Empty, @"[^\w]", "_");
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "Default";
            return $"Record_{sanitized}";
        }

        /// <summary>
        /// Ensure the per-book Record table exists.
        /// IMPORTANT: Align columns with your Record model.
        /// </summary>
        private async Task EnsureTableAsync(string bookName)
        {
            string table = GetRecordTableName(bookName);

            string sql = $@"
                CREATE TABLE IF NOT EXISTS ""{table}"" (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type TEXT,
                    Amount REAL NOT NULL,
                    Category TEXT,
                    Note TEXT,
                    Timestamp TEXT NOT NULL
                );";
            await _database.ExecuteAsync(sql);
        }

        public async Task<List<Record>> ListAsync(string bookName)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);

            string sql = $@"SELECT ID, Type, Amount, Category, Note, Timestamp
                            FROM ""{table}""
                            ORDER BY Timestamp DESC;";
            return await _database.QueryAsync<Record>(sql);
        }

        public async Task<Record> GetByIdAsync(string bookName, int id)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);

            string sql = $@"SELECT ID, Type, Amount, Category, Note, Timestamp
                            FROM ""{table}""
                            WHERE ID = ?;";
            // QueryAsync returns a list; here we just take first or default.
            var list = await _database.QueryAsync<Record>(sql, id);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task SaveAsync(string bookName, Record record)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);

            if (record.ID == 0)
            {
                string insertSql = $@"
                    INSERT INTO ""{table}"" (Type, Amount, Category, Note, Timestamp)
                    VALUES (?, ?, ?, ?, ?);";

                await _database.ExecuteAsync(
                    insertSql,
                    record.Type,
                    record.Amount,
                    record.Category,
                    record.Note,
                    // If your model uses DateTime, consider record.Timestamp.ToString("o")
                    record.Timestamp
                );

                // Set generated ID back to model (optional)
                var id = await _database.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                record.ID = (int)id;
            }
            else
            {
                string updateSql = $@"
                    UPDATE ""{table}""
                    SET Type = ?, Amount = ?, Category = ?, Note = ?, Timestamp = ?
                    WHERE ID = ?;";
                await _database.ExecuteAsync(
                    updateSql,
                    record.Type,
                    record.Amount,
                    record.Category,
                    record.Note,
                    record.Timestamp,
                    record.ID
                );
            }
        }

        public async Task DeleteAsync(string bookName, Record record)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);

            string sql = $@"DELETE FROM ""{table}"" WHERE ID = ?;";
            await _database.ExecuteAsync(sql, record.ID);
        }

        public async Task DeleteAllAsync(string bookName)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);

            string sql = $@"DELETE FROM ""{table}"";";
            await _database.ExecuteAsync(sql);
        }

        public async Task<List<Record>> GetRecordsPagedAsync(string bookName, int pageNumber, int pageSize)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);

            int skip = Math.Max(0, (pageNumber - 1) * pageSize);

            string sql = $@"
                SELECT ID, Type, Amount, Category, Note, Timestamp
                FROM ""{table}""
                ORDER BY Timestamp DESC
                LIMIT ? OFFSET ?;";
            return await _database.QueryAsync<Record>(sql, pageSize, skip);
        }
    }
}
