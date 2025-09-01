using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Resources.Strings; 

public static class CategoryData
{
    public static List<CategoryModel> GetExpenseCategories() => new()
    {
        new() { Name = AppResources.Category_Food,         Icon = "food.png" },
        new() { Name = AppResources.Category_Transport,    Icon = "transport.png" },
        new() { Name = AppResources.Category_Shopping,     Icon = "shopping.png" },
        new() { Name = AppResources.Category_Entertainment,Icon = "entertainment.png" },
        new() { Name = AppResources.Category_Medical,      Icon = "medical.png" },
        new() { Name = AppResources.Category_Education,    Icon = "education.png" },
        new() { Name = AppResources.Category_Digital,      Icon = "camera.png" },
    };

    public static List<CategoryModel> GetIncomeCategories() => new()
    {
        new() { Name = AppResources.Category_Salary, Icon = "salary.png" },
        new() { Name = AppResources.Category_Bonus,  Icon = "bonus.png" },
    };
}
