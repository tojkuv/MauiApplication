using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;

namespace MauiApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string title = "Settings";

    public SettingsViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await _navigationService.GoBackAsync();
    }
}