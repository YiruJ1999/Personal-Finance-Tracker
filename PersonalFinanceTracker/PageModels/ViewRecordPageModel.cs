using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Messages;
using Microsoft.Maui.Storage;

namespace PersonalFinanceTracker.PageModels
{
    public partial class ViewRecordPageModel : ObservableObject
    {
        private readonly DatabaseService _dbService;
        private readonly RecordRepository _recordRepository;

        // Persisted active book key (must match other pages)
        private const string PrefKeyCurrentBook = "current_book";

        // Paging state
        private int _currentPage = 1;
        private bool _isLoading = false;
        private int _pageSize = 15; // Default page size

        public ViewRecordPageModel(DatabaseService dbService, RecordRepository recordRepository)
        {
            _dbService = dbService;
            _recordRepository = recordRepository;

            Records = new ObservableCollection<Record>();

            // Listen for "record saved" events to refresh the list automatically
            WeakReferenceMessenger.Default.Register<RecordSavedMessage>(this, async (_, __) =>
            {
                await ResetAndReloadAsync();
            });
        }

        // -------- State --------

        /// <summary>
        /// The active book name; repository calls will target this book's table.
        /// </summary>
        [ObservableProperty]
        private string currentBook;

        /// <summary>
        /// The paged observable collection bound to UI.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Record> records;

        // -------- Commands / Lifecycle --------

        /// <summary>
        /// Page appearing: ensure DB is ready, set current book, then load first page.
        /// </summary>
        [RelayCommand]
        public async Task Appearing()
        {
            await _dbService.InitAsync();
            CurrentBook = Preferences.Default.Get(PrefKeyCurrentBook, "Default");
            await ResetAndReloadAsync();
        }

        /// <summary>
        /// Load next page (infinite scroll / "Load more" pattern).
        /// </summary>
        [RelayCommand]
        public async Task LoadNextPage()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var page = await _recordRepository.GetRecordsPagedAsync(CurrentBook, _currentPage, _pageSize);
                if (page != null && page.Count > 0)
                {
                    foreach (var r in page)
                        Records.Add(r);

                    _currentPage++; // advance page only when we received some data
                }
            }
            catch (Exception ex)
            {
                await AppShell.DisplaySnackbarAsync($"加载更多失败：{ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        // -------- Helpers --------

        /// <summary>
        /// Reset paging to page 1 and reload first page.
        /// </summary>
        private async Task ResetAndReloadAsync()
        {
            _currentPage = 1;
            Records.Clear();
            await LoadNextPage();
        }

        /// <summary>
        /// Optional: load entire list at once (not recommended for very large datasets).
        /// </summary>
        public async Task LoadFinancialData()
        {
            //System.Diagnostics.Debug.WriteLine("LoadFinancialData");
            try
            {
                var list = await _recordRepository.ListAsync(CurrentBook);
                Records = new ObservableCollection<Record>(list);
            }
            catch (Exception ex)
            {
                await AppShell.DisplaySnackbarAsync($"Error loading records: {ex.Message}");
            }
        }
    }
}
