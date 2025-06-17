namespace MauiApp.Views.TimeTracking;

public partial class TimeTrackingPage : ContentPage
{
    public TimeTrackingPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}