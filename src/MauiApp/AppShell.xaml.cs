using MauiApp.Views.Authentication;
using MauiApp.ViewModels;
using MauiApp.Services;

namespace MauiApp;

public partial class AppShell : Shell
{
    public AppShell(AppShellViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        
        RegisterRoutes();
    }

    private void RegisterRoutes()
    {
        // Authentication routes
        Routing.RegisterRoute("WelcomePage", typeof(WelcomePage));
        Routing.RegisterRoute("LoginPage", typeof(LoginPage));
        Routing.RegisterRoute("RegisterPage", typeof(RegisterPage));
        
        // Main application routes  
        Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(DiagnosticsPage), typeof(DiagnosticsPage));
        
        // Dashboard routes
        Routing.RegisterRoute("dashboard/overview", typeof(Views.Dashboard.DashboardPage));
        Routing.RegisterRoute("dashboard/mytasks", typeof(Views.Dashboard.MyTasksPage));
        Routing.RegisterRoute("dashboard/activity", typeof(Views.Dashboard.ActivityPage));
        
        // Feature routes
        Routing.RegisterRoute("projects/list", typeof(Views.Projects.ProjectsListPage));
        Routing.RegisterRoute("projects/detail", typeof(Views.Projects.ProjectDetailPage));
        Routing.RegisterRoute("projects/create", typeof(Views.Projects.CreateProjectPage));
        
        Routing.RegisterRoute("tasks/list", typeof(Views.Tasks.TasksPage));
        Routing.RegisterRoute("tasks/kanban", typeof(Views.Tasks.KanbanBoardPage));
        Routing.RegisterRoute("tasks/detail", typeof(Views.Tasks.TaskDetailPage));
        Routing.RegisterRoute("tasks/create", typeof(Views.Tasks.CreateTaskPage));
        
        Routing.RegisterRoute("collaboration/chat", typeof(Views.Collaboration.ChatPage));
        Routing.RegisterRoute("collaboration/channels", typeof(Views.Collaboration.ChannelsPage));
        
        Routing.RegisterRoute("files/browser", typeof(Views.Files.FilesPage));
        Routing.RegisterRoute("files/detail", typeof(Views.Files.FileDetailPage));
        
        Routing.RegisterRoute("reports/dashboard", typeof(Views.Reports.ReportsPage));
        Routing.RegisterRoute("reports/project", typeof(Views.Reports.ProjectReportPage));
        
        Routing.RegisterRoute("timetracking/timer", typeof(Views.TimeTracking.TimeTrackingPage));
        Routing.RegisterRoute("timetracking/timesheet", typeof(Views.TimeTracking.TimesheetPage));
    }
}