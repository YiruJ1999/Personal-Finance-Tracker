```csharp
namespace PersonalFinanceApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AddRecordPage());
    }

    private async void OnStatsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new StatsPage());
    }
}
```