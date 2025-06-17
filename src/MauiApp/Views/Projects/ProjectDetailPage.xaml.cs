namespace MauiApp.Views.Projects;

public partial class ProjectDetailPage : ContentPage
{
    public ProjectDetailPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}