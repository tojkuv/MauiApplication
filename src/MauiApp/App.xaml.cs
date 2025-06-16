using MauiApp.Services;

namespace MauiApp;

public partial class App : Application
{
    public App(INavigationService navigationService)
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}