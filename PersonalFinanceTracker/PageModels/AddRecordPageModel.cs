using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Messages;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace PersonalFinanceTracker.PageModels
{
    public partial class AddRecordPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly RecordRepository _recordRepository;

        // Keep the same key used in MainPageModel to persist the active book
        private const string PrefKeyCurrentBook = "current_book";

        public AddRecordPageModel(DatabaseService dbService, RecordRepository recordRepository)
        {
            _dbService = dbService;
            _recordRepository = recordRepository;

            // Default to Expense
            IsExpenseSelected = true;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            SelectedDate = DateTime.Now;

            // Initialize CurrentBook from preferences (fallback to "Default")
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBook, "Default");
        }

        // -------- State --------

        /// <summary>
        /// The active book name; all CRUD will target table for this book.
        /// </summary>
        [ObservableProperty]
        private string currentBook;

        /// <summary>
        /// Whether the record is an expense (true) or income (false).
        /// </summary>
        [ObservableProperty]
        private bool isExpenseSelected;

        /// <summary>
        /// Current category list bound to UI.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<CategoryModel> categories;

        /// <summary>
        /// Selected category item from the list.
        /// </summary>
        [ObservableProperty]
        private CategoryModel selectedCategory;

        /// <summary>
        /// Amount input as string (binds to Entry).
        /// </summary>
        [ObservableProperty]
        private string amount;

        /// <summary>
        /// Optional note for the record.
        /// </summary>
        [ObservableProperty]
        private string note;

        /// <summary>
        /// Selected date/time for the record.
        /// </summary>
        [ObservableProperty]
        private DateTime selectedDate;

        public object RecordType { get; private set; }

        // -------- Commands --------

        /// <summary>
        /// Page lifecycle: ensure DB connection and refresh CurrentBook from preferences.
        /// </summary>
        [RelayCommand]
        public async Task Appearing()
        {
            await _dbService.InitAsync();
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBook, "Default");
        }

        /// <summary>
        /// Switch to expense categories.
        /// </summary>
        [RelayCommand]
        private void SelectExpense()
        {
            IsExpenseSelected = true;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            SelectedCategory = null; // reset selection to avoid stale category
        }

        /// <summary>
        /// Switch to income categories.
        /// </summary>
        [RelayCommand]
        private void SelectIncome()
        {
            IsExpenseSelected = false;
            Categories = new ObservableCollection<CategoryModel>(CategoryData.GetIncomeCategories());
            SelectedCategory = null; // reset selection to avoid stale category
        }

        /// <summary>
        /// Save the record into the per-book Record table.
        /// </summary>
        [RelayCommand]
        private async Task Save()
        {
            // Basic validation: amount and category required
            if (string.IsNullOrWhiteSpace(Amount) || SelectedCategory == null)
            {
                // No UI alert here to keep ViewModel clean; UI can bind to validation states if needed.
                return;
            }

            // Parse using current culture (user types with local decimal separator)
            if (!decimal.TryParse(Amount.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amt))
            {
                return;
            }

            // Normalize timestamp; prefer local time with explicit kind
            var ts = DateTime.SpecifyKind(SelectedDate, DateTimeKind.Local);

            var record = new Record
            {
                Amount = amt,
                Category = SelectedCategory.Name,
                Note = Note,
                Timestamp = ts,
                Type = IsExpenseSelected ? "支出" : "收入"
            };

            // IMPORTANT: Pass the active book name so repository writes to the correct table
            await _recordRepository.SaveAsync(CurrentBook, record);

            // Notify other pages/viewmodels to refresh
            WeakReferenceMessenger.Default.Send(new RecordSavedMessage());

            // Navigate back
            await Shell.Current.GoToAsync("..");

            // Optionally reset inputs after save (uncomment if desired)
            // Amount = string.Empty;
            // Note = string.Empty;
            // SelectedCategory = null;
            // IsExpenseSelected = true;
            // Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
            // SelectedDate = DateTime.Now;
        }
    }
}
