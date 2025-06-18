using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Core.DTOs;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Reports;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IApiService _apiService;
    private readonly ILogger<ReportsViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private AdvancedDashboardDto dashboard = new();

    [ObservableProperty]
    private ObservableCollection<ChartData> charts = new();

    [ObservableProperty]
    private ObservableCollection<PredictiveInsightDto> insights = new();

    [ObservableProperty]
    private ObservableCollection<AlertDto> alerts = new();

    [ObservableProperty]
    private SystemHealthDto systemHealth = new();

    [ObservableProperty]
    private PerformanceMetricsDto performanceMetrics = new();

    [ObservableProperty]
    private ObservableCollection<ProjectAnalyticsDto> projectAnalytics = new();

    [ObservableProperty]
    private ObservableCollection<UserProductivityDto> userProductivity = new();

    [ObservableProperty]
    private DateTime selectedStartDate = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime selectedEndDate = DateTime.Today;

    [ObservableProperty]
    private string selectedTimeRange = "Last 30 Days";

    [ObservableProperty]
    private string selectedReportType = "Dashboard";

    [ObservableProperty]
    private Guid? selectedProjectId;

    [ObservableProperty]
    private string selectedProjectName = "All Projects";

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ReportTemplateDto> reportTemplates = new();

    [ObservableProperty]
    private ReportTemplateDto? selectedTemplate;

    [ObservableProperty]
    private bool showAdvancedOptions;

    [ObservableProperty]
    private string searchText = string.Empty;

    public List<string> TimeRangeOptions { get; } = new()
    {
        "Last 7 Days", "Last 30 Days", "Last 90 Days", "Last 6 Months", "Last Year", "Custom"
    };

    public List<string> ReportTypeOptions { get; } = new()
    {
        "Dashboard", "Projects", "Users", "Performance", "Time Tracking", "Business Intelligence"
    };

    public ReportsViewModel(
        IAnalyticsService analyticsService,
        IApiService apiService,
        ILogger<ReportsViewModel> logger)
    {
        _analyticsService = analyticsService;
        _apiService = apiService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;

            // Load initial data
            await LoadDashboardAsync();
            await LoadReportTemplatesAsync();
            await LoadInsightsAsync();
            await LoadAlertsAsync();

            _logger.LogInformation("Reports initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing reports");
            HasError = true;
            ErrorMessage = "Failed to initialize reports. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadDataBasedOnTypeAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        try
        {
            IsLoading = true;

            Dashboard = await _analyticsService.GetAdvancedDashboardAsync(SelectedStartDate, SelectedEndDate);
            
            // Load supporting data
            await LoadSystemHealthAsync();
            await LoadPerformanceMetricsAsync();

            // Generate charts based on dashboard data
            await GenerateChartsAsync();

            _logger.LogInformation("Dashboard loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard");
            HasError = true;
            ErrorMessage = "Failed to load dashboard data.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadProjectAnalyticsAsync()
    {
        try
        {
            IsLoading = true;

            List<ProjectAnalyticsDto> analytics;
            if (SelectedProjectId.HasValue)
            {
                var singleAnalytics = await _analyticsService.GetProjectAnalyticsAsync(
                    SelectedProjectId.Value, SelectedStartDate, SelectedEndDate);
                analytics = new List<ProjectAnalyticsDto> { singleAnalytics };
            }
            else
            {
                analytics = await _analyticsService.GetProjectsAnalyticsAsync(
                    null, SelectedStartDate, SelectedEndDate);
            }

            ProjectAnalytics.Clear();
            foreach (var item in analytics)
            {
                ProjectAnalytics.Add(item);
            }

            await GenerateProjectChartsAsync();
            _logger.LogInformation("Project analytics loaded: {Count} projects", analytics.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project analytics");
            HasError = true;
            ErrorMessage = "Failed to load project analytics.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadUserProductivityAsync()
    {
        try
        {
            IsLoading = true;

            var productivity = await _analyticsService.GetUsersProductivityAsync(
                null, SelectedStartDate, SelectedEndDate);

            UserProductivity.Clear();
            foreach (var item in productivity)
            {
                UserProductivity.Add(item);
            }

            await GenerateUserChartsAsync();
            _logger.LogInformation("User productivity loaded: {Count} users", productivity.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user productivity");
            HasError = true;
            ErrorMessage = "Failed to load user productivity data.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadSystemHealthAsync()
    {
        try
        {
            SystemHealth = await _analyticsService.GetSystemHealthAsync();
            _logger.LogInformation("System health loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load system health");
        }
    }

    [RelayCommand]
    private async Task LoadPerformanceMetricsAsync()
    {
        try
        {
            PerformanceMetrics = await _analyticsService.GetPerformanceMetricsAsync(SelectedStartDate, SelectedEndDate);
            _logger.LogInformation("Performance metrics loaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load performance metrics");
        }
    }

    [RelayCommand]
    private async Task LoadInsightsAsync()
    {
        try
        {
            var insightsList = await _analyticsService.GetPredictiveInsightsAsync(SelectedProjectId);
            
            Insights.Clear();
            foreach (var insight in insightsList)
            {
                Insights.Add(insight);
            }

            _logger.LogInformation("Predictive insights loaded: {Count} insights", insightsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load insights");
        }
    }

    [RelayCommand]
    private async Task LoadAlertsAsync()
    {
        try
        {
            var alertsList = await _analyticsService.GetActiveAlertsAsync();
            
            Alerts.Clear();
            foreach (var alert in alertsList)
            {
                Alerts.Add(alert);
            }

            _logger.LogInformation("Alerts loaded: {Count} alerts", alertsList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load alerts");
        }
    }

    [RelayCommand]
    private async Task LoadReportTemplatesAsync()
    {
        try
        {
            var templates = await _analyticsService.GetReportTemplatesAsync();
            
            ReportTemplates.Clear();
            foreach (var template in templates)
            {
                ReportTemplates.Add(template);
            }

            _logger.LogInformation("Report templates loaded: {Count} templates", templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load report templates");
        }
    }

    [RelayCommand]
    private async Task ChangeTimeRangeAsync(string timeRange)
    {
        SelectedTimeRange = timeRange;
        
        var now = DateTime.Today;
        (SelectedStartDate, SelectedEndDate) = timeRange switch
        {
            "Last 7 Days" => (now.AddDays(-7), now),
            "Last 30 Days" => (now.AddDays(-30), now),
            "Last 90 Days" => (now.AddDays(-90), now),
            "Last 6 Months" => (now.AddMonths(-6), now),
            "Last Year" => (now.AddYears(-1), now),
            _ => (SelectedStartDate, SelectedEndDate) // Custom - keep current dates
        };

        await LoadDataBasedOnTypeAsync();
    }

    [RelayCommand]
    private async Task ChangeReportTypeAsync(string reportType)
    {
        SelectedReportType = reportType;
        await LoadDataBasedOnTypeAsync();
    }

    [RelayCommand]
    private async Task ChangeProjectAsync(Guid? projectId, string projectName)
    {
        SelectedProjectId = projectId;
        SelectedProjectName = projectName;
        await LoadDataBasedOnTypeAsync();
    }

    [RelayCommand]
    private async Task GenerateCustomReportAsync()
    {
        try
        {
            IsLoading = true;

            var reportType = SelectedReportType.ToLower().Replace(" ", "");
            var request = new ReportGenerationRequest
            {
                ReportType = reportType,
                StartDate = SelectedStartDate,
                EndDate = SelectedEndDate,
                ProjectIds = SelectedProjectId.HasValue ? new List<Guid> { SelectedProjectId.Value } : new List<Guid>(),
                Format = "json",
                IncludeCharts = true
            };

            var report = await _analyticsService.GenerateCustomReportAsync(request);
            
            await Shell.Current.DisplayAlert("Success", $"Custom report '{report.Name}' generated successfully", "OK");
            _logger.LogInformation("Custom report generated: {ReportName}", report.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate custom report");
            await Shell.Current.DisplayAlert("Error", "Failed to generate custom report", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportReportAsync(string format)
    {
        try
        {
            IsLoading = true;

            var exportData = await _analyticsService.ExportDashboardAsync(format, null, SelectedProjectId);
            
            if (!string.IsNullOrEmpty(exportData))
            {
                await Shell.Current.DisplayAlert("Success", $"Report exported in {format.ToUpper()} format", "OK");
                _logger.LogInformation("Report exported in format: {Format}", format);
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Export failed - no data returned", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export report in format: {Format}", format);
            await Shell.Current.DisplayAlert("Error", $"Failed to export report in {format} format", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DismissAlertAsync(AlertDto alert)
    {
        try
        {
            await _analyticsService.DismissAlertAsync(alert.Id);
            Alerts.Remove(alert);
            _logger.LogInformation("Alert dismissed: {AlertId}", alert.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss alert: {AlertId}", alert.Id);
        }
    }

    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        ShowAdvancedOptions = !ShowAdvancedOptions;
    }

    [RelayCommand]
    private void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private async Task LoadDataBasedOnTypeAsync()
    {
        switch (SelectedReportType)
        {
            case "Dashboard":
                await LoadDashboardAsync();
                break;
            case "Projects":
                await LoadProjectAnalyticsAsync();
                break;
            case "Users":
                await LoadUserProductivityAsync();
                break;
            case "Performance":
                await LoadPerformanceMetricsAsync();
                break;
            case "Business Intelligence":
                await LoadDashboardAsync();
                break;
            default:
                await LoadDashboardAsync();
                break;
        }
    }

    private async Task GenerateChartsAsync()
    {
        try
        {
            Charts.Clear();

            // Project Status Chart
            var projectStatusChart = new ChartData
            {
                Type = "pie",
                Title = "Project Status Distribution",
                Labels = new List<string> { "Active", "Completed", "On Hold", "Delayed" },
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Projects",
                        Data = new List<double>
                        {
                            Dashboard.ProjectStats.ActiveProjects,
                            Dashboard.ProjectStats.CompletedProjects,
                            Dashboard.ProjectStats.DelayedProjects,
                            Dashboard.ProjectStats.DelayedProjects
                        },
                        BackgroundColor = "#4CAF50,#2196F3,#FF9800,#F44336"
                    }
                }
            };
            Charts.Add(projectStatusChart);

            // Task Velocity Chart
            var taskVelocityChart = new ChartData
            {
                Type = "line",
                Title = "Task Velocity Trend",
                Labels = new List<string> { "Week 1", "Week 2", "Week 3", "Week 4" },
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Tasks Completed",
                        Data = new List<double> { Dashboard.TaskStats.TaskVelocity * 7, Dashboard.TaskStats.TaskVelocity * 7 * 1.1, Dashboard.TaskStats.TaskVelocity * 7 * 0.9, Dashboard.TaskStats.TaskVelocity * 7 * 1.2 },
                        BorderColor = "#2196F3"
                    }
                }
            };
            Charts.Add(taskVelocityChart);

            // User Engagement Chart
            var userEngagementChart = new ChartData
            {
                Type = "bar",
                Title = "User Engagement",
                Labels = new List<string> { "Active Users", "Total Users" },
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Users",
                        Data = new List<double> { Dashboard.UserStats.ActiveUsers, Dashboard.UserStats.TotalUsers },
                        BackgroundColor = "#4CAF50,#2196F3"
                    }
                }
            };
            Charts.Add(userEngagementChart);

            _logger.LogInformation("Charts generated: {Count} charts", Charts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate charts");
        }

        await Task.CompletedTask;
    }

    private async Task GenerateProjectChartsAsync()
    {
        try
        {
            Charts.Clear();

            if (!ProjectAnalytics.Any()) return;

            // Project Completion Rate Chart
            var completionChart = new ChartData
            {
                Type = "bar",
                Title = "Project Completion Rates",
                Labels = ProjectAnalytics.Select(p => p.ProjectName).ToList(),
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Completion Rate (%)",
                        Data = ProjectAnalytics.Select(p => p.CompletionRate).ToList(),
                        BackgroundColor = "#4CAF50"
                    }
                }
            };
            Charts.Add(completionChart);

            // Task Distribution Chart
            var taskChart = new ChartData
            {
                Type = "doughnut",
                Title = "Task Distribution",
                Labels = new List<string> { "Completed", "In Progress", "Overdue" },
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Tasks",
                        Data = new List<double>
                        {
                            ProjectAnalytics.Sum(p => p.CompletedTasks),
                            ProjectAnalytics.Sum(p => p.InProgressTasks),
                            ProjectAnalytics.Sum(p => p.OverdueTasks)
                        },
                        BackgroundColor = "#4CAF50,#2196F3,#F44336"
                    }
                }
            };
            Charts.Add(taskChart);

            _logger.LogInformation("Project charts generated: {Count} charts", Charts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate project charts");
        }

        await Task.CompletedTask;
    }

    private async Task GenerateUserChartsAsync()
    {
        try
        {
            Charts.Clear();

            if (!UserProductivity.Any()) return;

            // User Productivity Chart
            var productivityChart = new ChartData
            {
                Type = "bar",
                Title = "User Productivity Scores",
                Labels = UserProductivity.Take(10).Select(u => u.UserName).ToList(),
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Productivity Score",
                        Data = UserProductivity.Take(10).Select(u => u.ProductivityScore).ToList(),
                        BackgroundColor = "#2196F3"
                    }
                }
            };
            Charts.Add(productivityChart);

            // Tasks Completed Chart
            var tasksChart = new ChartData
            {
                Type = "line",
                Title = "Tasks Completed by Top Users",
                Labels = UserProductivity.Take(5).Select(u => u.UserName).ToList(),
                Datasets = new List<ChartDataset>
                {
                    new ChartDataset
                    {
                        Label = "Tasks Completed",
                        Data = UserProductivity.Take(5).Select(u => (double)u.TasksCompleted).ToList(),
                        BorderColor = "#4CAF50"
                    }
                }
            };
            Charts.Add(tasksChart);

            _logger.LogInformation("User charts generated: {Count} charts", Charts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate user charts");
        }

        await Task.CompletedTask;
    }

    partial void OnSelectedTimeRangeChanged(string value)
    {
        _ = Task.Run(async () => await ChangeTimeRangeAsync(value));
    }

    partial void OnSelectedReportTypeChanged(string value)
    {
        _ = Task.Run(async () => await ChangeReportTypeAsync(value));
    }
}