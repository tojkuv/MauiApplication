namespace MauiApp.Views.Files;

public partial class FilesPage : ContentPage
{
    public FilesPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}