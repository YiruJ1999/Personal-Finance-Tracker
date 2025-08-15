using SQLite;
using System;

namespace PersonalFinanceTracker.Models
{
    public class Book
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Human readable name (can be Chinese)
        public string Name { get; set; } = string.Empty;

        // Physical SQLite table name, e.g. "book_默认"
        // Always quoted when used in SQL.
        public string TableName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
