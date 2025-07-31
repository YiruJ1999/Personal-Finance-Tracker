using SQLite;
using System;

namespace PersonalFinanceTracker.Models
{
    [Table("Records")]
    public class Record
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        // Income or expenditure
        public string Type { get; set; }  

        // Amount
        public decimal Amount { get; set; }

        // Category
        public string Category { get; set; }

        // Note
        public string Note { get; set; }

        // Timestamp of the record
        public DateTime Timestamp { get; set; }
    }
}
