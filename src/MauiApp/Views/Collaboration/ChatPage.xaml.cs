namespace MauiApp.Views.Collaboration;

public partial class ChatPage : ContentPage
{
    public ChatPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}