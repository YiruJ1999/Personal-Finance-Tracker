using CommunityToolkit.Maui.Views;

namespace PersonalFinanceTracker.PageModels;

public partial class RecordDetailPageModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _dbService;
    private readonly RecordRepository _recordRepository;
    private readonly BookRepository _bookRepository;
    private readonly AccountRepository _accountRepository;

    // Legacy name-based preference key (kept for backward compatibility)
    private const string PrefKeyCurrentBookName = "current_book";
    // New id-based preference key (source of truth)
    private const string PrefKeyCurrentBookId = "current_book_id";

    public RecordDetailPageModel(
        DatabaseService dbService,
        RecordRepository recordRepository,
        BookRepository bookRepository,
        AccountRepository accountRepository)
    {
        _dbService = dbService;
        _recordRepository = recordRepository;
        _bookRepository = bookRepository;
        _accountRepository = accountRepository;
        Record = new();
        
    }

    // -------- Bindable state --------

    [ObservableProperty] private int bookId;

    [ObservableProperty] private string currentBook = "д╛хо";

    [ObservableProperty] private Record record = new();

    [ObservableProperty] private string ? accountName;

    // -------- Navigation / Lifecycle --------

    /// <summary>
    /// Shell passes query params after navigation. We expect at least 'recordId';
    /// 'bookId' is preferred, otherwise we fall back to Preferences.
    /// </summary>
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        await _dbService.InitAsync(); // ensure DB is ready

        // recordId is required
        if (!(query.TryGetValue("recordId", out var rid) && int.TryParse(rid?.ToString(), out var recordId)))
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", "recordId is missing.", "OK");
            return;
        }

        // get bookId from query, otherwise fallback to preferences
        if (query.TryGetValue("bookId", out var bval) && int.TryParse(bval?.ToString(), out var bid))
        {
            BookId = bid;
        }
        else
        {
            BookId = Preferences.Default.Get(PrefKeyCurrentBookId, 0);
        }

        var book = await _bookRepository.GetBookByIdAsync(BookId);
        CurrentBook = book?.Name ?? "д╛хо";

        await LoadAsync(BookId, recordId);
    }

    /// <summary>
    /// Load record data by (bookId, recordId).
    /// </summary>
    private async Task LoadAsync(int bookId, int id)
    {
        Record = await _recordRepository.GetByIdAsync(bookId, id) ?? new Record();

        var book = await _bookRepository.GetBookByIdAsync(bookId);
        CurrentBook = book?.Name ?? "д╛хо";

        if (Record.AccountId > 0)
        {
            var account = await _accountRepository.GetAccountByIdAsync(Record.AccountId);
            AccountName = account?.Name ?? $"#{Record.AccountId}";
        }
        else
        {
            AccountName = "/";
        }

    }

    // -------- Commands --------

    [RelayCommand]
    private async Task Edit()
    {
        // guard if not loaded
        if (Record is null || Record.Id <= 0) return;

        // Show the edit popup and wait for result
        
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Record?.Id <= 0) return;

        // Open confirm popup; expect a bool? result
        var confirmObj = await Application.Current!.MainPage!.ShowPopupAsync(
            new DeleteRecordPopup(Record.Id)          // adapt to your popup ctor
        );

        if (confirmObj is not bool confirmed || !confirmed) return;

        await _recordRepository.DeleteAsync(BookId, Record);

        try { await AppShell.DisplaySnackbarAsync("Record deleted"); }
        catch { await Application.Current!.MainPage!.DisplayAlert("Info", "Record deleted", "OK"); }

        WeakReferenceMessenger.Default.Send(new RecordDeletedMessage());
        await Shell.Current.GoToAsync("..", true);
    }

    [RelayCommand]
    private async Task Back()
    {
        
    }
}
