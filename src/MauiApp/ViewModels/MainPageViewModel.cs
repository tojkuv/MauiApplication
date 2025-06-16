using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;

namespace MauiApp.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private int count = 0;

    [ObservableProperty]
    private string text = "Click me";

    public MainPageViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    [RelayCommand]
    private void OnCounterClicked()
    {
        Count++;

        if (Count == 1)
            Text = $"Clicked {Count} time";
        else
            Text = $"Clicked {Count} times";

        SemanticScreenReader.Announce(Text);
    }

    [RelayCommand]
    private async Task NavigateToSettings()
    {
        await _navigationService.NavigateToAsync("SettingsPage");
    }
}