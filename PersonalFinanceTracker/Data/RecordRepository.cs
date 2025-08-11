using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
//using Intents;

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
            return $"book_{sanitized}";
        }

        public async Task CreateNewTable(string bookName)
        {
            await EnsureTableAsync(bookName);
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
                    Timestamp TEXT NOT NULL,
                    Account TEXT NOT NULL
                );";
            await _database.ExecuteAsync(sql);
        }

        public async Task<List<Record>> ListAsync(string bookName)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table); 

            string sql = $@"
                SELECT ID, Type, Amount, Category, Note, Timestamp, Account
                FROM ""{table}""
                ORDER BY Timestamp DESC;";
            return await _database.QueryAsync<Record>(sql);
        }


        public async Task<Record> GetByIdAsync(string bookName, int id)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table);

            string sql = $@"SELECT ID, Type, Amount, Category, Note, Timestamp, Account
                            FROM ""{table}""
                            WHERE ID = ?;";
            // QueryAsync returns a list; here we just take first or default.
            var list = await _database.QueryAsync<Record>(sql, id);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task SaveAsync(string bookName, Record record)
        {

            record.Account = string.IsNullOrWhiteSpace(record.Account) ? "现金" : record.Account.Trim();

            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table);   
            if (record.ID == 0)
            {
                // 1) INSERT must include Account column
                string insertSql = $@"
            INSERT INTO ""{table}""
                (Type, Amount, Category, Note, Timestamp, Account)
            VALUES (?, ?, ?, ?, ?, ?);";

                await _database.ExecuteAsync(
                    insertSql,
                    record.Type,
                    record.Amount,
                    record.Category,
                    record.Note,
                    record.Timestamp,   
                    record.Account      
                );

                // 2) Set generated ID back to model (optional)
                var id = await _database.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                record.ID = (int)id;
            }
            else
            {
                // 3) UPDATE must set Account too
                string updateSql = $@"
                    UPDATE ""{table}""
                    SET Type = ?, Amount = ?, Category = ?, Note = ?, Timestamp = ?, Account = ?
                    WHERE ID = ?;";

                await _database.ExecuteAsync(
                    updateSql,
                    record.Type,
                    record.Amount,
                    record.Category,
                    record.Note,
                    record.Timestamp,
                    record.Account,  
                    record.ID
                );
            }
        }

        public async Task DeleteAsync(string bookName, Record record)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table); 

            string sql = $@"DELETE FROM ""{table}"" WHERE ID = ?;";
            await _database.ExecuteAsync(sql, record.ID);
        }

        public async Task DeleteAllAsync(string bookName)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table); 

            string sql = $@"DELETE FROM ""{table}"";";
            await _database.ExecuteAsync(sql);
        }

        public async Task<List<Record>> GetRecordsPagedAsync(string bookName, int pageNumber, int pageSize)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table);

            int skip = Math.Max(0, (pageNumber - 1) * pageSize);

            string sql = $@"
                SELECT ID, Type, Amount, Category, Note, Timestamp, Account
                FROM ""{table}""
                ORDER BY Timestamp DESC
                LIMIT ? OFFSET ?;";
            return await _database.QueryAsync<Record>(sql, pageSize, skip);
        }

        private class TableInfoRow
        {
            public int cid { get; set; }   // column id
            public string name { get; set; }   // column name
            public string type { get; set; }   // column type
        }
        private async Task EnsureAccountColumnAsync(string table)
        {
            // Query current schema
            var info = await _database.QueryAsync<TableInfoRow>($@"PRAGMA table_info(""{table}"");");

            bool hasAccount = info.Any(c => string.Equals(c.name, "Account", StringComparison.OrdinalIgnoreCase));
            if (!hasAccount)
            {
                // Add the missing column
                await _database.ExecuteAsync($@"ALTER TABLE ""{table}"" ADD COLUMN Account TEXT;");

                // Backfill existing rows to avoid NULL accounts breaking your aggregation logic
                await _database.ExecuteAsync($@"UPDATE ""{table}"" SET Account='现金' WHERE Account IS NULL OR TRIM(Account)='';");
            }
        }
    }
}
