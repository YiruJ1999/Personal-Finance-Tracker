using PersonalFinanceTracker.PageModels;

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
            PickerTitle = "选择头像",
            FileTypes = FilePickerFileType.Images
        });

        if (result != null)
        {
            pageModel.AvatarPath = result.FullPath;
        }
    }

    private async void OnEditNameClicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync("修改用户名", "请输入新的用户名", initialValue: pageModel.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            pageModel.Name = name;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        await pageModel.SaveAsync();
        await DisplayAlert("成功", "信息已保存", "确定");
    }
}
