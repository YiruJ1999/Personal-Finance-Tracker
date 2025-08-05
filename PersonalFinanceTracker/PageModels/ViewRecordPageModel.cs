using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalFinanceTracker.Messages;



namespace PersonalFinanceTracker.PageModels;
public partial class ViewRecordPageModel : ObservableObject
{
    private readonly DatabaseService _dbService;
    private readonly RecordRepository _recordRepository;
    private int _currentPage = 1;
    private bool _isLoading = false;
    private int _pageSize = 15; // Default page size

    public ViewRecordPageModel(DatabaseService dbService, RecordRepository recordRepository)
    {
        _dbService = dbService;
        _recordRepository = recordRepository;
        Records = new ObservableCollection<Record>();
    }

    [RelayCommand]
    public async Task Appearing()
    {
        await _dbService.InitAsync();

        await LoadNextPage();

    }

    [RelayCommand]
    public async Task LoadNextPage()
    {
        if (_isLoading) return;

        _isLoading = true;
        var records = await _dbService.GetRecordsPagedAsync(_currentPage++, _pageSize);
        foreach (var record in records)
        {
            Records.Add(record);
        }
        _isLoading = false;
    }
    public async Task LoadFinancialData()
    {
        System.Diagnostics.Debug.WriteLine("LoadFinancialData");
        try
        {
            // Load all records from the database
            var records = await _recordRepository.ListAsync();
            Records = new ObservableCollection<Record>(records);
        }
        catch (Exception ex)
        {
            // Handle exceptions, e.g., show an error message
            await AppShell.DisplaySnackbarAsync($"Error loading records: {ex.Message}");
        }
    }


    [ObservableProperty]
    private ObservableCollection<Record> records;

    



}
