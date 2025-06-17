namespace MauiApp.Views.Tasks;

public partial class CreateTaskPage : ContentPage
{
    public CreateTaskPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}