using PersonalFinanceTracker.Resources.Strings;
using CommunityToolkit.Maui.Views;
using System.Text.RegularExpressions;

namespace PersonalFinanceTracker.Pages;

public partial class BuggetPopup : Popup
{

    public string Amount { get; private set; } = string.Empty;
    public BuggetPopup()
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


    private void AmountEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 只允许数字和一个小数点
        var entry = sender as Entry;
        if (entry == null) return;

        string text = entry.Text ?? string.Empty;
        string filtered = Regex.Replace(text, @"[^0-9.]", "");

        // 只允许一个小数点
        int dotIndex = filtered.IndexOf('.');
        if (dotIndex >= 0)
        {
            filtered = filtered.Substring(0, dotIndex + 1) +
                       filtered.Substring(dotIndex + 1).Replace(".", "");
        }

        if (filtered != text)
            entry.Text = filtered;
    }

}
