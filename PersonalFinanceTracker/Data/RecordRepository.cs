using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace PersonalFinanceTracker.Data
{
    public class RecordRepository
    {
        private readonly DatabaseService _database;
        private readonly BookRepository _books;

        public RecordRepository(DatabaseService database, BookRepository books)
        {
            _database = database;
            _books = books;
        }

        // Quote helper
        private static string Q(string ident) => BookRepository.QuoteIdent(ident);

        /// <summary>
        /// Ensure the physical book table exists for a given bookId (idempotent).
        /// </summary>
        public async Task EnsureTableAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
        }

        /// <summary>
        /// Migration helper: backfill AccountId using Account.Name for rows where AccountId is null/0.
        /// Will create missing accounts if needed.
        /// </summary>
        private sealed class AccountNameRow
        {
            // Property name MUST match the SQL column alias
            public string Account { get; set; } = string.Empty;
        }

        private async Task BackfillAccountIdsAsync(string table)
        {
            var q = Q(table); // your QuoteIdent helper

            // 1) Resolve existing names → Ids (use a DTO instead of a 1-tuple)
            var missing = await _database.QueryAsync<AccountNameRow>(
                $@"SELECT DISTINCT Account AS Account
             FROM {q}
            WHERE (AccountId IS NULL OR AccountId = 0)
              AND Account IS NOT NULL
              AND TRIM(Account) <> '';");

            if (missing.Count == 0) return;

            foreach (var row in missing)
            {
                var name = (row.Account ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name)) continue;

                // 2) Ensure the account exists (case-insensitive lookup)
                var accounts = await _database.QueryAsync<Account>(
                    @"SELECT * FROM Account WHERE Name = ? COLLATE NOCASE LIMIT 1;",
                    name);

                var acc = accounts.FirstOrDefault();
                if (acc == null)
                {
                    acc = new Account { Name = name, Balance = 0m, CreatedAt = DateTime.UtcNow };
                    await _database.InsertAsync(acc); // auto-increments Id
                }

                // 3) Backfill AccountId for rows matching this name
                await _database.ExecuteAsync(
                    $@"UPDATE {q}
                  SET AccountId = ?
                WHERE (AccountId IS NULL OR AccountId = 0)
                  AND Account = ? COLLATE NOCASE;",
                    acc.Id, name);
            }
        }
        public async Task<List<Record>> ListAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await BackfillAccountIdsAsync(table);

            var sql = $@"
                SELECT Id, Type, Amount, Category, Note, Timestamp, Account, AccountId
                FROM {Q(table)}
                ORDER BY Timestamp DESC;";
            return await _database.QueryAsync<Record>(sql);
        }

        public async Task<Record?> GetByIdAsync(int bookId, int id)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await BackfillAccountIdsAsync(table);

            var list = await _database.QueryAsync<Record>(
                $@"SELECT Id, Type, Amount, Category, Note, Timestamp, Account, AccountId
                   FROM {Q(table)} WHERE Id = ?;", id);
            return list.FirstOrDefault();
        }

        public async Task SaveAsync(int bookId, Record record)
        {
            // Require AccountId
            if (record.AccountId <= 0)
                throw new InvalidOperationException("Record.AccountId must be set to a valid Account Id.");

            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);

            // Derive Account name for display (optional but nice to have)
            var accRow = await _database.FindAsync<Account>(record.AccountId);
            var displayName = accRow?.Name ?? (record.Account ?? "默认");

            if (record.Timestamp.Kind == DateTimeKind.Unspecified)
                record.Timestamp = DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Local);

            if (record.Id == 0)
            {
                string insertSql = $@"
                    INSERT INTO {Q(table)}
                        (Type, Amount, Category, Note, Timestamp, Account, AccountId)
                    VALUES (?, ?, ?, ?, ?, ?, ?);";
                await _database.ExecuteAsync(insertSql,
                    record.Type, record.Amount, record.Category, record.Note, record.Timestamp, displayName, record.AccountId);

                var id = await _database.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                record.Id = (int)id;
            }
            else
            {
                string updateSql = $@"
                    UPDATE {Q(table)}
                       SET Type = ?, Amount = ?, Category = ?, Note = ?, Timestamp = ?, Account = ?, AccountId = ?
                     WHERE Id = ?;";
                await _database.ExecuteAsync(updateSql,
                    record.Type, record.Amount, record.Category, record.Note, record.Timestamp, displayName, record.AccountId, record.Id);
            }
        }

        public async Task DeleteAsync(int bookId, Record record)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await _database.ExecuteAsync($@"DELETE FROM {Q(table)} WHERE Id = ?;", record.Id);
        }

        public async Task DeleteAllAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await _database.ExecuteAsync($@"DELETE FROM {Q(table)};");
        }

        public async Task DropBookTableAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _database.ExecuteAsync($@"DROP TABLE IF EXISTS {Q(table)};");
        }

        public async Task<List<Record>> GetRecordsPagedAsync(int bookId, int pageNumber, int pageSize)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await BackfillAccountIdsAsync(table);

            int skip = Math.Max(0, (pageNumber - 1) * pageSize);

            var sql = $@"
                SELECT Id, Type, Amount, Category, Note, Timestamp, Account, AccountId
                FROM {Q(table)}
                ORDER BY Timestamp DESC
                LIMIT ? OFFSET ?;";
            return await _database.QueryAsync<Record>(sql, pageSize, skip);
        }
    }
}
