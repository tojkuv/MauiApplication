namespace MauiApp.Views.Collaboration;

public partial class ChannelsPage : ContentPage
{
    public ChannelsPage()
    {
        InitializeComponent();
    }

    public Command BackCommand => new Command(async () => await Shell.Current.GoToAsync(".."));
}