```csharp
namespace PersonalFinanceApp.Models;

public class Record
{
    public int Id { get; set; }
    public string Type { get; set; } // "收入" 或 "支出"
    public decimal Amount { get; set; }
    public string Category { get; set; }
    public string Remark { get; set; }
    public DateTime Timestamp { get; set; }
}
```