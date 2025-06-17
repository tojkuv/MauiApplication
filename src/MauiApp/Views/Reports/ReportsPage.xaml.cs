namespace MauiApp.Views.Reports;

public partial class ReportsPage : ContentPage
{
    public ReportsPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}