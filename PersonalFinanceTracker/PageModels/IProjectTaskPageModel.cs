using CommunityToolkit.Mvvm.Input;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}