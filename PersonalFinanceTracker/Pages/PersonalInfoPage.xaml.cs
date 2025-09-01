using PersonalFinanceTracker.PageModels;
using PersonalFinanceTracker.Resources.Strings;

namespace PersonalFinanceTracker.Pages;

public partial class PersonalInfoPage : ContentPage
{
    private readonly PersonalInfoPageModel pageModel = new PersonalInfoPageModel();

    public PersonalInfoPage()
    {
        InitializeComponent();
        BindingContext = pageModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await pageModel.LoadAsync();
    }

    private async void OnEditAvatarClicked(object sender, EventArgs e)
    {
        var result = await FilePicker.PickAsync(new PickOptions
        {
            PickerTitle = AppResources.Dialog_PickerTitle,
            FileTypes = FilePickerFileType.Images
        });

        if (result != null)
        {
            pageModel.AvatarPath = result.FullPath;
        }
    }

    private async void OnEditNameClicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync(
            AppResources.Dialog_EditUsernameTitle,   
            AppResources.Dialog_EditUsernamePrompt,  
            initialValue: pageModel.Name);

        if (!string.IsNullOrWhiteSpace(name))
        {
            pageModel.Name = name;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        await pageModel.SaveAsync();
        await DisplayAlert(
            AppResources.Dialog_SaveSuccessTitle, 
            AppResources.Dialog_SaveSuccessMsg,   
            AppResources.Dialog_OK);
    }
}
