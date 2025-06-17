namespace MauiApp.Views.Tasks;

public partial class KanbanBoardPage : ContentPage
{
    public KanbanBoardPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}