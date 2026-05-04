// Personal Finance Tracker
// File: PersonalFinanceTracker/PageModels/BookPageModel.cs
// Purpose: Coordinates page state, commands, navigation, and data loading for a MAUI page.

using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using PersonalFinanceTracker.Services;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Pages;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.PageModels
{
    public partial class BookPageModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly IServiceProvider _sp;
        private readonly BookRepository _bookRepository;

        // Preference keys (id-based is the source of truth; name is kept for display/back-compat)
        private const string PrefKeyCurrentBookId = "current_book_id";
        private const string PrefKeyCurrentBookName = "current_book";
        private const string DefaultBookDisplayName = "默认";

        public BookPageModel(DatabaseService db, IServiceProvider sp, BookRepository bookRepository)
        {
            _db = db;
            _sp = sp;
            _bookRepository = bookRepository;

            Books = new ObservableCollection<BookSummary>();
        }

        [ObservableProperty]
        private ObservableCollection<BookSummary> books;

        [ObservableProperty]
        private BookSummary? selectedBook;

        partial void OnSelectedBookChanged(BookSummary? value)
        {
            if (value is null) return;

            // Persist both id (new) and name (legacy display)
            Preferences.Default.Set(PrefKeyCurrentBookId, value.BookId);
            Preferences.Default.Set(PrefKeyCurrentBookName, value.BookName);
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
            var popup = _sp.GetRequiredService<CreateNewBookPopup>();
            var result = await Shell.Current.ShowPopupAsync(popup);

            if (result is string newBookName && !string.IsNullOrWhiteSpace(newBookName))
            {
                // Ensure or create the book by display name (supports Chinese)
                var book = await _bookRepository.EnsureBookAsync(newBookName.Trim());

                // Persist current selection (id + name)
                Preferences.Default.Set(PrefKeyCurrentBookId, book.Id);
                Preferences.Default.Set(PrefKeyCurrentBookName, book.Name);

                await LoadAllBooksAsync();
                await AppShell.DisplayToastAsync($"已创建账本：{book.Name}");
                return;
            }

            HighlightSelectedBook();
        }

        /// <summary>
        /// Load all books from the Books table and compute aggregates per physical table.
        /// </summary>
        private async Task LoadAllBooksAsync()
        {
            Books.Clear();

            // 1) list logical books
            var logicalBooks = await _bookRepository.ListBooksAsync();

            // 2) for each book, aggregate from its physical table
            foreach (var b in logicalBooks)
            {
                var q = BookRepository.QuoteIdent(b.TableName);

                // Aggregate sums and last modified (MAX of Timestamp text; ISO 8601 works lexicographically)
                var rows = await _db.QueryAsync<_AggRow>($@"
                    SELECT
                        COALESCE(SUM(CASE WHEN Type = '收入' THEN Amount ELSE 0 END), 0) AS IncomeAmount,
                        COALESCE(SUM(CASE WHEN Type = '支出' THEN Amount ELSE 0 END), 0) AS ExpenseAmount,
                        MAX(Timestamp) AS LastModified
                    FROM {q};");

                var agg = rows.Count > 0 ? rows[0] : new _AggRow();

                DateTime? last = null;
                if (!string.IsNullOrWhiteSpace(agg.LastModified) && long.TryParse(agg.LastModified, out var parsed))
                    last = new DateTime(parsed);

                // Budget: prefer id-based key, fallback once to legacy name-based key if needed
                var budget = (decimal)Preferences.Default.Get(BudgetKeyById(b.Id),
                              Preferences.Default.Get(BudgetKeyByLegacyName(b.Name), 0.0));

                Books.Add(new BookSummary
                {
                    BookId = b.Id,
                    BookName = b.Name,
                    IncomeAmount = agg.IncomeAmount,
                    ExpenseAmount = agg.ExpenseAmount,
                    BalanceAmount = agg.IncomeAmount - agg.ExpenseAmount,
                    LastModified = last,
                    BudgetAmount = budget
                });
            }
        }

        // Delete current book (drop physical table and remove Book row)
        [RelayCommand]
        private async Task DropBook(BookSummary? item)
        {
            if (item is null) return;
            var name = item.BookName;

            bool ok = await Application.Current.MainPage.DisplayAlert(
                "删除账本",
                $"确定删除“{name}”吗？该账本的所有记录将被永久移除！",
                "删除", "取消");
            if (!ok) return;

            try
            {
                await _bookRepository.DeleteBookAsync(item.BookId, dropPhysical: true);

                // Remove per-book settings if any (budget)
                Preferences.Default.Remove(BudgetKeyById(item.BookId));
                Preferences.Default.Remove(BudgetKeyByLegacyName(item.BookName));

                // Refresh list
                await LoadAllBooksAsync();

                // If deleted was the current book, pick another or clear
                var curId = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
                if (curId == item.BookId)
                {
                    var first = Books.FirstOrDefault();
                    if (first != null)
                    {
                        SelectedBook = first;
                        Preferences.Default.Set(PrefKeyCurrentBookId, first.BookId);
                        Preferences.Default.Set(PrefKeyCurrentBookName, first.BookName);
                    }
                    else
                    {
                        SelectedBook = null;
                        Preferences.Default.Remove(PrefKeyCurrentBookId);
                        Preferences.Default.Remove(PrefKeyCurrentBookName);
                    }
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
            var curId = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
            if (curId > 0)
            {
                SelectedBook = Books.FirstOrDefault(b => b.BookId == curId);
                return;
            }

            // Fallback to legacy name
            var legacyName = Preferences.Default.Get(PrefKeyCurrentBookName, DefaultBookDisplayName);
            SelectedBook = Books.FirstOrDefault(b => string.Equals(b.BookName, legacyName, StringComparison.OrdinalIgnoreCase));
        }

        private static string BudgetKeyById(int bookId) => $"monthlybugget_{bookId}";
        private static string BudgetKeyByLegacyName(string name) => $"monthlybugget_{name}";

        // DTO for aggregation query
        private sealed class _AggRow
        {
            public decimal IncomeAmount { get; set; }
            public decimal ExpenseAmount { get; set; }
            public string LastModified { get; set; } = string.Empty;
        }
    }

    public class BookSummary
    {
        public int BookId { get; set; }            // <-- added: use id everywhere
        public string BookName { get; set; } = "";
        public DateTime? LastModified { get; set; }
        public decimal IncomeAmount { get; set; }
        public decimal ExpenseAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public decimal BudgetAmount { get; set; }
    }
}

