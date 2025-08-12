using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System;
using System.Linq; // for .Any()

namespace PersonalFinanceTracker.Data
{
    public class RecordRepository
    {
        private readonly DatabaseService _database;

        public RecordRepository(DatabaseService database)
        {
            _database = database;
        }

        private static string GetRecordTableName(string bookName)
        {
            string sanitized = Regex.Replace(bookName ?? string.Empty, @"[^\w]", "_");
            if (string.IsNullOrWhiteSpace(sanitized))
                sanitized = "Default"; // keep a single canonical default
            return $"book_{sanitized}";
        }

        // Normalize account name: replace full-width spaces, trim, and fallback to "默认".
        private static string NormalizeAccountName(string? name)
        {
            var n = (name ?? string.Empty).Replace('\u3000', ' ').Trim();
            return string.IsNullOrWhiteSpace(n) ? "默认" : n;
        }

        public async Task CreateNewTable(string bookName)
        {
            await EnsureTableAsync(bookName);
        }


        // Ensure the per-book Record table exists.
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
            await EnsureAccountColumnAsync(table); // normalize legacy data if needed

            string sql = $@"
                SELECT ID, Type, Amount, Category, Note, Timestamp, Account
                FROM ""{table}""
                ORDER BY Timestamp DESC;";
            return await _database.QueryAsync<Record>(sql);
        }

        public async Task<Record?> GetByIdAsync(string bookName, int id)
        {
            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table);

            string sql = $@"SELECT ID, Type, Amount, Category, Note, Timestamp, Account
                            FROM ""{table}""
                            WHERE ID = ?;";
            var list = await _database.QueryAsync<Record>(sql, id);
            return list.Count > 0 ? list[0] : null;
        }

        public async Task SaveAsync(string bookName, Record record)
        {
            // Always normalize the account name before persisting
            record.Account = NormalizeAccountName(record.Account);

            // (Optional) Ensure Timestamp is local-kind if unspecified
            if (record.Timestamp.Kind == DateTimeKind.Unspecified)
                record.Timestamp = DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Local);

            string table = GetRecordTableName(bookName);
            await EnsureTableAsync(bookName);
            await EnsureAccountColumnAsync(table);

            if (record.ID == 0)
            {
                // INSERT
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

                // Set generated ID back to model
                var id = await _database.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                record.ID = (int)id;
            }
            else
            {
                // UPDATE
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

        public async Task DropBookAsync(string bookName)
        {
            string table = GetRecordTableName(bookName);
            string sql = $@"DROP TABLE IF EXISTS ""{table}""";
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
            public int cid { get; set; }     // column id
            public string name { get; set; } // column name
            public string type { get; set; } // column type
        }

        // Ensure "Account" column exists; additionally sanitize legacy data:
        private async Task EnsureAccountColumnAsync(string table)
        {
            var info = await _database.QueryAsync<TableInfoRow>($@"PRAGMA table_info(""{table}"");");
            bool hasAccount = info.Any(c => string.Equals(c.name, "Account", StringComparison.OrdinalIgnoreCase));

            if (!hasAccount)
            {
                await _database.ExecuteAsync($@"ALTER TABLE ""{table}"" ADD COLUMN Account TEXT;");
            }

            // Normalize existing rows regardless of whether the column was newly added:
            // 1) Replace full-width spaces and trim
            await _database.ExecuteAsync($@"
                UPDATE ""{table}""
                   SET Account = TRIM(REPLACE(IFNULL(Account, ''), '　', ' '))
            ");

            // 2) Backfill blanks as "默认"
            await _database.ExecuteAsync($@"
                UPDATE ""{table}""
                   SET Account = '默认'
                 WHERE Account IS NULL OR TRIM(Account) = ''
            ");
        }
    }
}
