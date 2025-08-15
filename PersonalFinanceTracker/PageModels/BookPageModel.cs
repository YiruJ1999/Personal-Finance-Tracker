using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PersonalFinanceTracker.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace PersonalFinanceTracker.PageModels
{
    public partial class BookPageModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly IServiceProvider _sp;
        private readonly RecordRepository _recordRepository;

        public BookPageModel(DatabaseService db, IServiceProvider sp, RecordRepository recordRepository)
        {
            _db = db;
            _sp = sp;
            Books = new ObservableCollection<BookSummary>();
            _recordRepository = recordRepository;
        }

        [ObservableProperty]
        private ObservableCollection<BookSummary> books;

        [ObservableProperty]
        private BookSummary? selectedBook;

        partial void OnSelectedBookChanged(BookSummary? value)
        {
            if (value is null) return;

            // persist the current book name
            Preferences.Default.Set("current_book", value.BookName);
        }

        [RelayCommand]
        public async Task Appearing()
        {
            await _db.InitAsync();
            await LoadAllBooksAsync();
            HighlightSelectedBook();
        }

        [RelayCommand]
        private async Task OnAddBookClicked()
        {
            //System.Diagnostics.Debug.WriteLine("AddBookClickedCommand");

            var popup = _sp.GetRequiredService<CreateNewBookPopup>();
            var result = await Shell.Current.ShowPopupAsync(popup);

            // If CreateNewBookPopup returns the new book name (recommended)
            if (result is string newBookName && !string.IsNullOrWhiteSpace(newBookName))
            {
                // Create new book
                await _recordRepository.CreateNewTable(newBookName);
                Preferences.Default.Set("current_book", newBookName);
                
                await LoadAllBooksAsync();
                await AppShell.DisplayToastAsync($"已创建账本：{newBookName}");
                return;
            }

            HighlightSelectedBook();

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
                DateTime? last = null;

                if (!string.IsNullOrWhiteSpace(first.LastModified) &&
                    long.TryParse(first.LastModified,out var ticks))
                {
                    System.Diagnostics.Debug.WriteLine("IF-Condition fulfilled");
                    last = new DateTime(ticks,DateTimeKind.Local);
                }

                System.Diagnostics.Debug.WriteLine(first.LastModified);
                last = last ?? DateTime.MinValue; // fallback to MinValue if parsing fails

                // Budget: try per-book key first, fallback to global MonthlyBugget
                var perBookBudgetKey = $"monthlybugget_{bookName}";
                var budget = (decimal)Preferences.Default.Get(perBookBudgetKey,
                                0);

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

        // Delete current book table
        [RelayCommand]
        private async Task DropBook(BookSummary? item)
        {
            await AppShell.DisplayToastAsync("DropBook fired");

            if (item is null || string.IsNullOrWhiteSpace(item.BookName))
                return;

            var name = item.BookName;

            bool ok = await Application.Current.MainPage.DisplayAlert(
                "删除账本",
                $"确定删除“{name}”吗？该账本的所有记录将被永久移除！",
                "删除", "取消");

            if (!ok) return;

            try
            {
                await _recordRepository.DropBookAsync(name);

                // Remove per-book settings if any (budget example)
                // Preferences.Default.Remove($"budget_{name}");

                // Refresh list
                await LoadAllBooksAsync();

                // If deleted was the current book, pick another or clear
                var current = Preferences.Default.Get("current_book", string.Empty);
                if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedBook = Books.FirstOrDefault();
                    Preferences.Default.Set("current_book", SelectedBook?.BookName ?? string.Empty);
                }

                await AppShell.DisplayToastAsync($"已删除账本：{name}");
            }
            catch (Exception ex)
            {
                await AppShell.DisplaySnackbarAsync($"删除失败：{ex.Message}");
            }
        }

        private void HighlightSelectedBook()
        {
            System.Diagnostics.Debug.WriteLine("HighlightSelectedBook()");
            foreach (var book in Books)
            {
                if (book.BookName == Preferences.Default.Get("current_book", ""))
                {
                    SelectedBook = book;
                }
                
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
