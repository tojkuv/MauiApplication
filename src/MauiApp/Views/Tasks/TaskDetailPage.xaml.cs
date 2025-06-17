namespace MauiApp.Views.Tasks;

public partial class TaskDetailPage : ContentPage
{
    public TaskDetailPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}