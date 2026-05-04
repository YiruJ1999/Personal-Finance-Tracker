// Personal Finance Tracker
// File: PersonalFinanceTracker/Models/Record.cs
// Purpose: Defines a finance domain model persisted or displayed by the app.

using SQLite;
using System;

namespace PersonalFinanceTracker.Models
{
    [Table("Records")]
    public class Record
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        // Foreign key to Account
        [Indexed]
        public int AccountId { get; set; }

        // Income or expenditure
        public string Type { get; set; } = string.Empty;

        // Amount
        public decimal Amount { get; set; }

        // Living expenses, entertainment, etc.
        public string Category { get; set; } = string.Empty;

        // Note
        public string Note { get; set; } = string.Empty;

        // Timestamp of the record
        public DateTime Timestamp { get; set; }

    }
}

