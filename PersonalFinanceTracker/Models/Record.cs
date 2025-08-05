using SQLite;
using System;

namespace PersonalFinanceTracker.Models
{
    [Table("Records")]
    public class Record
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        // Account 
        public string Account { get; set; } = string.Empty;

        // Income or expenditure
        public string Type { get; set; } = string.Empty;

        // Amount
        public decimal Amount { get; set; }

        // Category
        public string Category { get; set; } = string.Empty;

        // Note
        public string Note { get; set; } = string.Empty;

        // Timestamp of the record
        public DateTime Timestamp { get; set; }
    }
}
