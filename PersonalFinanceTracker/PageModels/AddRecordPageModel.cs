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
public partial class AddRecordPageModel : ObservableObject
{
    private readonly DatabaseService _dbService;

    public AddRecordPageModel(DatabaseService dbService)
    {
        _dbService = dbService;

        // Defalt to Expense
        IsExpenseSelected = true;
        Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
        SelectedDate = DateTime.Now;
    }

    // income or expense
    [ObservableProperty]
    private bool isExpenseSelected;

    // categories list
    [ObservableProperty]
    private ObservableCollection<CategoryModel> categories;

    // selected category
    [ObservableProperty]
    private CategoryModel selectedCategory;

    // amount entry
    [ObservableProperty]
    private string amount;

    // note entry
    [ObservableProperty]
    private string note;

    // date picker
    [ObservableProperty]
    private DateTime selectedDate;

    public object RecordType { get; private set; }

    // expense selection
    [RelayCommand]
    private void SelectExpense()
    {
        IsExpenseSelected = true;
        Categories = new ObservableCollection<CategoryModel>(CategoryData.GetExpenseCategories());
    }

    // income selection
    [RelayCommand]
    private void SelectIncome()
    {
        IsExpenseSelected = false;
        Categories = new ObservableCollection<CategoryModel>(CategoryData.GetIncomeCategories());
    }

    // save record command
    [RelayCommand]
    private async Task Save()
    {
        if (decimal.TryParse(Amount, out decimal amt) && SelectedCategory != null)
        {
            var record = new Record
            {
                Amount = amt,
                Category = SelectedCategory.Name,
                Note = Note,
                Timestamp = SelectedDate,
                Type = IsExpenseSelected ? "支出" : "收入"
            };

            await _dbService.SaveRecordAsync(record);

            // reset fields after saving
            WeakReferenceMessenger.Default.Send(new RecordSavedMessage());

            // go back to previous page
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            
      
        }
    }
}
