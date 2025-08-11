using CommunityToolkit.Maui.Views;
using System.Text.RegularExpressions;

namespace PersonalFinanceTracker.Pages;

public partial class CreateNewBookPopup : Popup
{

    public string Amount { get; private set; } = string.Empty;
    public CreateNewBookPopup()
    {
        InitializeComponent();
    }

    private void ConfirmClicked(object sender, EventArgs e)
    {
        Amount = AmountEntry.Text ?? string.Empty;
        Close(Amount); // 关闭弹窗并返回值输入值
    }

    private void CancelClicked(object sender, EventArgs e)
    {
        Close(null); // 关闭弹窗并返回值null
    }


}
