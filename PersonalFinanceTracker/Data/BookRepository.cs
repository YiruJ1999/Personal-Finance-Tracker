using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.Data
{
    public class BookRepository
    {
        private readonly SQLiteAsyncConnection _db;

        public BookRepository(DatabaseService database)
        {
            _db = database.Database;
        }

        // Quote an identifier to be safe in SQL.
        public static string QuoteIdent(string name) => "\"" + (name ?? string.Empty).Replace("\"", "\"\"") + "\"";

        // Allow Unicode letters & digits + underscore; replace others with '_'
        public static string BuildPhysicalTableName(string? rawName)
        {
            var raw = (rawName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(raw) || string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
                raw = "Default";
            var sanitized = Regex.Replace(raw, @"[^\p{L}\p{Nd}_]", "_");
            if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "Default";
            return $"book_{sanitized}";
        }

        public async Task EnsureBooksTableAsync()
        {
            await _db.CreateTableAsync<Book>();
        }

        /// <summary>
        /// Create a Book row and its physical table if not exists; return Book.
        /// </summary>
        public async Task<Book> EnsureBookAsync(string displayName)
        {
            await EnsureBooksTableAsync();

            // Try match by display name first
            var existing = await _db.Table<Book>().Where(b => b.Name == displayName).FirstOrDefaultAsync();
            if (existing != null)
            {
                await EnsureBookTableSchemaAsync(existing.TableName);
                return existing;
            }

            // Otherwise create a new one
            var tableName = BuildPhysicalTableName(displayName);
            var dup = await _db.Table<Book>().Where(b => b.TableName == tableName).FirstOrDefaultAsync();
            if (dup != null)
            {
                // Rare: same physical name. Append a suffix.
                tableName += "_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            var book = new Book { Name = displayName.Trim(), TableName = tableName, CreatedAt = DateTime.UtcNow };
            await _db.InsertAsync(book);
            await EnsureBookTableSchemaAsync(book.TableName);
            return book;
        }

        public async Task<Book?> GetBookByIdAsync(int bookId)
        {
            await EnsureBooksTableAsync();
            return await _db.FindAsync<Book>(bookId);
        }

        public async Task<string> GetTableNameByIdAsync(int bookId)
        {
            var book = await GetBookByIdAsync(bookId) ?? throw new InvalidOperationException($"Book not found: {bookId}");
            return book.TableName;
        }

        public async Task<List<Book>> ListBooksAsync()
        {
            await EnsureBooksTableAsync();
            return await _db.Table<Book>().OrderBy(b => b.Id).ToListAsync();
        }

        /// <summary>
        /// Ensure the per-book record table schema (idempotent).
        /// </summary>
        public async Task EnsureBookTableSchemaAsync(string tableName)
        {
            var q = QuoteIdent(tableName);
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {q} (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Type TEXT,
                    Amount REAL NOT NULL,
                    Category TEXT,
                    Note TEXT,
                    Timestamp TEXT NOT NULL,
                    Account TEXT,         -- kept for legacy/display
                    AccountId INTEGER     -- NEW: link to Account
                );
                ";
            await _db.ExecuteAsync(sql);

            // Add AccountId if missing; add indexes
            var cols = await _db.QueryAsync<(int cid, string name, string type)>($@"PRAGMA table_info({q});");
            if (!cols.Any(c => string.Equals(c.name, "AccountId", StringComparison.OrdinalIgnoreCase)))
            {
                await _db.ExecuteAsync($@"ALTER TABLE {q} ADD COLUMN AccountId INTEGER;");
            }
            await _db.ExecuteAsync($@"CREATE INDEX IF NOT EXISTS idx_{tableName}_accountid ON {q}(AccountId);");
            await _db.ExecuteAsync($@"CREATE INDEX IF NOT EXISTS idx_{tableName}_ts ON {q}(Timestamp);");
        }

        /// <summary>
        /// One-off migration: If Books table is empty but there are existing book_* tables,
        /// import them into Books with Name = suffix after 'book_' (best-effort for Chinese).
        /// </summary>
        public async Task ImportExistingPhysicalBooksIfAnyAsync()
        {
            await EnsureBooksTableAsync();
            var count = await _db.Table<Book>().CountAsync();
            if (count > 0) return;

            var names = await _db.QueryScalarsAsync<string>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'book_%';");

            foreach (var t in names)
            {
                var display = t.StartsWith("book_") ? t.Substring("book_".Length) : t;
                var exists = await _db.Table<Book>().Where(b => b.TableName == t).FirstOrDefaultAsync();
                if (exists == null)
                {
                    await _db.InsertAsync(new Book { Name = display, TableName = t, CreatedAt = DateTime.UtcNow });
                }
            }
        }

        // Delete a book: optionally drop its physical table, then remove the Book row
        public async Task DeleteBookAsync(int bookId, bool dropPhysical = true)
        {
            await EnsureBooksTableAsync();
            var book = await _db.FindAsync<Book>(bookId);
            if (book == null) return;

            if (dropPhysical)
            {
                await _db.ExecuteAsync($@"DROP TABLE IF EXISTS {QuoteIdent(book.TableName)};");
            }

            await _db.DeleteAsync(book);
        }

    }
}
