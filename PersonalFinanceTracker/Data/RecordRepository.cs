// Personal Finance Tracker
// File: PersonalFinanceTracker/Data/RecordRepository.cs
// Purpose: Encapsulates persistence and data-access behavior for the finance domain.

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
        private readonly DatabaseService _dbService;
        private readonly BookRepository _books;

        public RecordRepository(DatabaseService database, BookRepository books)
        {
            _dbService = database;
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

        public async Task<List<Record>> ListAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);

            var sql = $@"
                SELECT Id, Type, Amount, Category, Note, Timestamp, AccountId
                FROM {Q(table)}
                ORDER BY Timestamp DESC;";
            return await _dbService.QueryAsync<Record>(sql);
        }

        public async Task<Record?> GetByIdAsync(int bookId, int id)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);

            var list = await _dbService.QueryAsync<Record>(
                $@"SELECT Id, Type, Amount, Category, Note, Timestamp, AccountId
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
            var accRow = await _dbService.FindAsync<Account>(record.AccountId);
            var displayName = accRow?.Name ??  "默认";

            if (record.Timestamp.Kind == DateTimeKind.Unspecified)
                record.Timestamp = DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Local);
            record.Timestamp = record.Timestamp.ToUniversalTime();

            if (record.Id == 0)
            {
                string insertSql = $@"
                    INSERT INTO {Q(table)}
                        (Type, Amount, Category, Note, Timestamp, AccountId)
                    VALUES (?, ?, ?, ?, ?, ?);";
                await _dbService.ExecuteAsync(insertSql,
                    record.Type, record.Amount, record.Category, record.Note, record.Timestamp, record.AccountId);

                var id = await _dbService.ExecuteScalarAsync<long>("SELECT last_insert_rowid();");
                record.Id = (int)id;
            }
            else
            {
                string updateSql = $@"
                    UPDATE {Q(table)}
                       SET Type = ?, Amount = ?, Category = ?, Note = ?, Timestamp = ?,  AccountId = ?
                     WHERE Id = ?;";
                await _dbService.ExecuteAsync(updateSql,
                    record.Type, record.Amount, record.Category, record.Note, record.Timestamp, record.AccountId, record.Id);
            }
        }

        public async Task DeleteAsync(int bookId, Record record)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await _dbService.ExecuteAsync($@"DELETE FROM {Q(table)} WHERE Id = ?;", record.Id);
        }

        public async Task DeleteAllAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);
            await _dbService.ExecuteAsync($@"DELETE FROM {Q(table)};");
        }

        public async Task DropBookTableAsync(int bookId)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _dbService.ExecuteAsync($@"DROP TABLE IF EXISTS {Q(table)};");
        }

        // record paging with optional month filter
        public async Task<List<Record>> GetRecordsPagedAsync(int bookId, int pageNumber, int pageSize)
        {
            return await GetRecordsPagedAsync(bookId, pageNumber, pageSize, null, null);
        }
        public async Task<List<Record>> GetRecordsPagedAsync(int bookId, int pageNumber, int pageSize,
            DateTime? start,  DateTime? end)
        {
            var table = await _books.GetTableNameByIdAsync(bookId);
            await _books.EnsureBookTableSchemaAsync(table);

            int skip = Math.Max(0, (pageNumber - 1) * pageSize);

            var where = "";
            var args = new List<object>();

            if (start.HasValue && end.HasValue)
            {
                where = "WHERE Timestamp >= ? AND Timestamp < ?";
                args.Add(start.Value);
                args.Add(end.Value);
            }
            else if (start.HasValue)
            {
                where = "WHERE Timestamp >= ?";
                args.Add(start.Value);
            }
            else if (end.HasValue)
            {
                where = "WHERE Timestamp < ?";
                args.Add(end.Value);
            }

            var sql = $@"
                        SELECT Id, Type, Amount, Category, Note, Timestamp, AccountId
                        FROM {Q(table)}
                        {where}
                        ORDER BY Timestamp DESC, Id DESC
                        LIMIT ? OFFSET ?;";

            // append paging parameters after filter parameters
            args.Add(pageSize);
            args.Add(skip);

            return await _dbService.QueryAsync<Record>(sql, args.ToArray());
        }

        // Ensure an index on Timestamp to speed up month filtering + ORDER BY
        // Call this once when creating/upgrading the book table schema:
        private async Task EnsureTimestampIndexAsync(string table)
        {
            var sql = $"CREATE INDEX IF NOT EXISTS IDX_{Q(table)}_Timestamp ON {Q(table)}(Timestamp DESC);";
            await _dbService.ExecuteAsync(sql);
        }



    }
}

