using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PersonalFinanceTracker.PageModels;

public class PersonalInfoPageModel : INotifyPropertyChanged
{
    private readonly PersonalInfoRepository _repository = new PersonalInfoRepository(new Services.DatabaseService());

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private string _avatarPath = string.Empty;
    public string AvatarPath
    {
        get => _avatarPath;
        set
        {
            _avatarPath = value;
            Avatar = string.IsNullOrEmpty(value)
                ? null
                : ImageSource.FromFile(value);
            OnPropertyChanged();
        }
    }

    private ImageSource _avatar;
    public ImageSource Avatar
    {
        get => _avatar;
        set { _avatar = value; OnPropertyChanged(); }
    }

    private PersonalInfo? _loadedInfo;



    // load data
    public async Task LoadAsync()
    {
        await _repository.EnsureDatabaseInitializedAsync();
        _loadedInfo = await _repository.GetPersonalInfoAsync();
        if (_loadedInfo != null)
        {
            Name = _loadedInfo.Name;
            AvatarPath = _loadedInfo.AvatarPath;
        }
    }

    // save data
    public async Task SaveAsync()
    {
        if (_loadedInfo == null)
            _loadedInfo = new PersonalInfo();

        _loadedInfo.Name = Name;
        _loadedInfo.AvatarPath = AvatarPath;

        await _repository.SaveAsync(_loadedInfo);
    }


    public event PropertyChangedEventHandler PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
