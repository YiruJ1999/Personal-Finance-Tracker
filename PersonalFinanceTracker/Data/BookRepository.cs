using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;

namespace PersonalFinanceTracker.Data
{
    public class BookRepository
    {
        private readonly DatabaseService _dbService;

        public BookRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // Safely get a live SQLiteAsyncConnection; auto-init if needed
        private async Task<SQLiteAsyncConnection> ConnAsync()
        {
            if (_dbService.Database == null)
                await _dbService.InitAsync(); // ensure connection is created
            return _dbService.Database!;
        }

        // Quote an identifier to be safe in SQL.
        public static string QuoteIdent(string name) => "\"" + (name ?? string.Empty).Replace("\"", "\"\"") + "\"";

        // Allow Unicode letters & digits + underscore; replace others with '_'
        public static string BuildPhysicalTableName(string? rawName)
        {
            var raw = (rawName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
                raw = "默认";
            var sanitized = Regex.Replace(raw, @"[^\p{L}\p{Nd}_]", "_");
            if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "默认";
            return $"book_{sanitized}";
        }

        public async Task EnsureBooksTableAsync()
        {
            var conn = await ConnAsync();
            await conn.CreateTableAsync<Book>();
        }

        public async Task<Book> EnsureBookAsync(string? displayName)
        {
            var conn = await ConnAsync();
            await EnsureBooksTableAsync();

            // Guard display name
            var name = string.IsNullOrWhiteSpace(displayName) ? "默认" : displayName.Trim();

            // Try match by display name first
            var existing = await conn.Table<Book>().Where(b => b.Name == name).FirstOrDefaultAsync();
            if (existing != null)
            {
                await EnsureBookTableSchemaAsync(existing.TableName);
                return existing;
            }

            // Otherwise create a new one
            var tableName = BuildPhysicalTableName(name);
            var dup = await conn.Table<Book>().Where(b => b.TableName == tableName).FirstOrDefaultAsync();
            if (dup != null)
            {
                // Rare: same physical name. Append a suffix.
                tableName += "_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            var book = new Book { Name = name, TableName = tableName, CreatedAt = DateTime.UtcNow };
            await conn.InsertAsync(book);
            await EnsureBookTableSchemaAsync(book.TableName);
            return book;
        }

        public async Task<Book?> GetBookByIdAsync(int bookId)
        {
            await EnsureBooksTableAsync();
            var conn = await ConnAsync();
            return await conn.FindAsync<Book>(bookId);
        }

        public async Task<string> GetTableNameByIdAsync(int bookId)
        {
            var book = await GetBookByIdAsync(bookId) ?? throw new InvalidOperationException($"Book not found: {bookId}");
            return book.TableName;
        }

        public async Task<List<Book>> ListBooksAsync()
        {
            await EnsureBooksTableAsync();
            var conn = await ConnAsync();
            return await conn.Table<Book>().OrderBy(b => b.Id).ToListAsync();
        }

        /// <summary>
        /// Ensure the per-book record table schema (idempotent).
        /// </summary>
        public async Task EnsureBookTableSchemaAsync(string tableName)
        {
            var conn = await ConnAsync();
            var q = QuoteIdent(tableName);
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {q} (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type TEXT,
                    Amount REAL NOT NULL,
                    Category TEXT,
                    Note TEXT,
                    Timestamp TEXT NOT NULL,
                    AccountId INTEGER     -- link to Account
                );";
            await conn.ExecuteAsync(sql);

            // Ensure AccountId column and helpful indexes
            var cols = await conn.QueryAsync<(int cid, string name, string type)>($@"PRAGMA table_info({q});");
            if (!cols.Any(c => string.Equals(c.name, "AccountId", StringComparison.OrdinalIgnoreCase)))
            {
                await conn.ExecuteAsync($@"ALTER TABLE {q} ADD COLUMN AccountId INTEGER;");
            }
            await conn.ExecuteAsync($@"CREATE INDEX IF NOT EXISTS idx_{tableName}_accountid ON {q}(AccountId);");
            await conn.ExecuteAsync($@"CREATE INDEX IF NOT EXISTS idx_{tableName}_ts ON {q}(Timestamp);");
        }

        /// <summary>
        /// Import existing physical book_* tables into Books if Books table is empty.
        /// </summary>
        public async Task ImportExistingPhysicalBooksIfAnyAsync()
        {
            var conn = await ConnAsync();
            await EnsureBooksTableAsync();

            var count = await conn.Table<Book>().CountAsync();
            if (count > 0) return;

            var names = await conn.QueryScalarsAsync<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'book_%';");

            foreach (var t in names)
            {
                var display = t.StartsWith("book_") ? t.Substring("book_".Length) : t;
                var exists = await conn.Table<Book>().Where(b => b.TableName == t).FirstOrDefaultAsync();
                if (exists == null)
                {
                    await conn.InsertAsync(new Book { Name = display, TableName = t, CreatedAt = DateTime.UtcNow });
                }
            }
        }

        /// <summary>
        /// Delete a book: optionally drop its physical table, then remove the Book row.
        /// </summary>
        public async Task DeleteBookAsync(int bookId, bool dropPhysical = true)
        {
            var conn = await ConnAsync();
            await EnsureBooksTableAsync();

            var book = await conn.FindAsync<Book>(bookId);
            if (book == null) return;

            if (dropPhysical)
            {
                await conn.ExecuteAsync($@"DROP TABLE IF EXISTS {QuoteIdent(book.TableName)};");
            }

            await conn.DeleteAsync(book);
        }
    }
}
