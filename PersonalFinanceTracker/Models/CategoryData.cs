using PersonalFinanceTracker.Models;

public static class CategoryData
{
    public static List<CategoryModel> GetExpenseCategories() => new()
    {
        new() { Name = "饮食", Icon = "food.png" },
        new() { Name = "交通", Icon = "shopping.png" },
        new() { Name = "购物", Icon = "transport.png" },
        new() { Name = "娱乐", Icon = "entertainment.png" },
        new() { Name = "医疗", Icon = "medical.png" },
        new() { Name = "教育", Icon = "education.png" },
        new() { Name = "数码", Icon = "camera.png" },

    };

    public static List<CategoryModel> GetIncomeCategories() => new()
    {
        new() { Name = "工资", Icon = "salary.png" },
        new() { Name = "奖金", Icon = "bonus.png" },
        
    };
}
