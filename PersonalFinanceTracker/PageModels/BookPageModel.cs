using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.PageModels
{
    public partial class BookPageModel : ObservableObject
    {
        private readonly DatabaseService _db;

        public BookPageModel(DatabaseService db)
        {
            _db = db;
            Books = new ObservableCollection<BookSummary>();
        }

        [ObservableProperty]
        private ObservableCollection<BookSummary> books;

        [RelayCommand]
        public async Task Appearing()
        {
            await _db.InitAsync();
            await LoadAllBooksAsync();
        }

        /// <summary>
        /// Load all tables starting with 'book_' and compute aggregates for each.
        /// </summary>
        private async Task LoadAllBooksAsync()
        {
            Books.Clear();
            System.Diagnostics.Debug.WriteLine("LoadAllBooksAsync");
            // 1) enumerate all book tables: names start with 'book_'
            var tableRows = await _db.QueryAsync<SqliteNameRow>(
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'book\\_%' ESCAPE '\\';");

            foreach (var row in tableRows)
            {
                var tableName = row.name;
                System.Diagnostics.Debug.WriteLine("tableName");
                // Derive a display book name from table name, e.g. 'book_Family2025' -> 'Family2025'
                var bookName = Regex.Replace(tableName ?? string.Empty, @"^book_", string.Empty);

                // 2) query aggregates in one shot
                // NOTE:
                // - Type is assumed to be '收入' or '支出'
                // - Timestamp is stored as ISO 8601 text; MAX works lexicographically
                var sql = $@"
                    SELECT
                        COALESCE(SUM(CASE WHEN Type = '收入' THEN Amount ELSE 0 END), 0) AS IncomeAmount,
                        COALESCE(SUM(CASE WHEN Type = '支出' THEN Amount ELSE 0 END), 0) AS ExpenseAmount,
                        MAX(Timestamp) AS LastModified
                    FROM ""{tableName}"";";

                var agg = await _db.QueryAsync<AggregateRow>(sql);
                var first = agg.Count > 0 ? agg[0] : new AggregateRow();

                // Parse last modified (works if Timestamp is ISO 8601 string or SQLite datetime)
                DateTime? last = null;
                if (!string.IsNullOrWhiteSpace(first.LastModified) &&
                    DateTime.TryParse(first.LastModified, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    last = dt;
                }

                // Budget: try per-book key first, fallback to global MonthlyBugget
                var perBookBudgetKey = $"budget_{bookName}";
                var budget = (decimal)Preferences.Default.Get(perBookBudgetKey,
                                Preferences.Default.Get("MonthlyBugget", 0.0));

                Books.Add(new BookSummary
                {
                    BookName = bookName,
                    IncomeAmount = first.IncomeAmount,
                    ExpenseAmount = first.ExpenseAmount,
                    BalanceAmount = first.IncomeAmount - first.ExpenseAmount,
                    LastModified = last,
                    BudgetAmount = budget
                });
            }
        }

        // POCOs for raw SQL mapping
        private sealed class SqliteNameRow
        {
            // property name must match the selected column alias
            public string name { get; set; }
        }

        private sealed class AggregateRow
        {
            public decimal IncomeAmount { get; set; }
            public decimal ExpenseAmount { get; set; }
            public string LastModified { get; set; } // read as string; we parse to DateTime?
        }
    }

    public class BookSummary
    {
        public string BookName { get; set; }
        public DateTime? LastModified { get; set; }
        public decimal IncomeAmount { get; set; }
        public decimal ExpenseAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public decimal BudgetAmount { get; set; }
    }
}
