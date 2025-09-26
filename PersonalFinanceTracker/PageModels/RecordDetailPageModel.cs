using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;              // for Application.Current
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Messages;              // for RecordSavedMessage / RecordDeletedMessage
using PersonalFinanceTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.PageModels;

public partial class RecordDetailPageModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _dbService;
    private readonly RecordRepository _recordRepository;
    private readonly BookRepository _bookRepository;

    // Legacy name-based preference key (kept for backward compatibility)
    private const string PrefKeyCurrentBookName = "current_book";
    // New id-based preference key (source of truth)
    private const string PrefKeyCurrentBookId = "current_book_id";

    public RecordDetailPageModel(
        DatabaseService dbService,
        RecordRepository recordRepository,
        BookRepository bookRepository)
    {
        _dbService = dbService;
        _recordRepository = recordRepository;
        _bookRepository = bookRepository;

        // keep Record non-null so compiled bindings do not see 'Record?'
        Record = new();
    }

    // -------- Bindable state --------

    /// <summary>
    /// Active book id (table context for repository).
    /// </summary>
    [ObservableProperty]
    private int bookId;

    /// <summary>
    /// Display name of the active book (optional, for header UI).
    /// </summary>
    [ObservableProperty]
    private string currentBook = "д╛хо";

    /// <summary>
    /// The record currently shown on the detail page (non-null for compiled bindings).
    /// </summary>
    [ObservableProperty]
    private Record record = new();

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

        // optional: show current book name if you want in the header (use literal default to avoid self-reference)
        CurrentBook = Preferences.Default.Get(PrefKeyCurrentBookName, "д╛хо");

        await LoadAsync(BookId, recordId);
    }

    /// <summary>
    /// Load record data by (bookId, recordId).
    /// </summary>
    private async Task LoadAsync(int bookId, int id)
    {
        Record = await _recordRepository.GetByIdAsync(bookId, id) ?? new Record();
    }

    // -------- Commands --------

    [RelayCommand]
    private async Task Edit()
    {
        // guard if not loaded
        if (Record is null || Record.Id <= 0) return;

        // TODO: open an edit page/sheet; after saving, notify list pages to refresh
        // NOTE: your RecordSavedMessage in ViewModel registration looks like a marker message (no payload),
        // so send it without arguments.
        WeakReferenceMessenger.Default.Send(new RecordSavedMessage());
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Record is null || Record.Id <= 0) return;

        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Confirm", "Delete this record?", "Yes", "No");

        if (!confirm) return;


        await _recordRepository.DeleteAsync(BookId, Record);
        // var rows = await _recordRepository.DeleteAsync(Record.Id);

        try { await AppShell.DisplaySnackbarAsync("Record deleted"); }
        catch { await Application.Current!.MainPage!.DisplayAlert("Info", "Record deleted", "OK"); }

        WeakReferenceMessenger.Default.Send(new RecordDeletedMessage());
        await Shell.Current.GoToAsync("..", true);
    }
}
