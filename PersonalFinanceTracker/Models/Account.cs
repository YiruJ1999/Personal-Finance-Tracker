// Personal Finance Tracker
// File: PersonalFinanceTracker/Models/Account.cs
// Purpose: Defines a finance domain model persisted or displayed by the app.

using SQLite;
using System;


namespace PersonalFinanceTracker.Models
{
    [Table("Accounts")]

    public partial class Account
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        // Account name
        public string Name { get; set; } = string.Empty;
        // Account balance
        public decimal Balance { get; set; } = 0.0m;
        // Account type (e.g., Savings, Checking)
        public string Type { get; set; } = string.Empty;
        // Date of account creation
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

