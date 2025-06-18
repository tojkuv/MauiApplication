using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Core.DTOs;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace MauiApp.ViewModels.Files;

public partial class FilesViewModel : ObservableObject
{
    private readonly IApiService _apiService;
    private readonly ILogger<FilesViewModel> _logger;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isUploading;

    [ObservableProperty]
    private Guid selectedProjectId;

    [ObservableProperty]
    private string projectName = string.Empty;

    [ObservableProperty]
    private string currentFolderPath = string.Empty;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProjectFileDto> files = new();

    [ObservableProperty]
    private ObservableCollection<string> folders = new();

    [ObservableProperty]
    private ProjectFileDto? selectedFile;

    [ObservableProperty]
    private FileStorageStatsDto? storageStats;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string viewMode = "grid"; // grid, list

    [ObservableProperty]
    private string sortBy = "CreatedAt";

    [ObservableProperty]
    private bool sortDescending = true;

    [ObservableProperty]
    private double uploadProgress;

    [ObservableProperty]
    private string uploadStatus = string.Empty;

    public FilesViewModel(IApiService apiService, ILogger<FilesViewModel> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task InitializeAsync(Guid projectId)
    {
        try
        {
            IsLoading = true;
            HasError = false;
            SelectedProjectId = projectId;

            // Load project info
            await LoadProjectInfoAsync();

            // Load files and folders
            await LoadFilesAsync();

            // Load storage statistics
            await LoadStorageStatsAsync();

            _logger.LogInformation("Files initialized for project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing files for project {ProjectId}", projectId);
            HasError = true;
            ErrorMessage = "Failed to load files. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadFilesAsync()
    {
        try
        {
            IsLoading = true;

            var searchRequest = new FileSearchRequest
            {
                ProjectId = SelectedProjectId,
                FolderPath = string.IsNullOrEmpty(CurrentFolderPath) ? null : CurrentFolderPath,
                SearchTerm = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                SortBy = SortBy,
                SortDescending = SortDescending,
                PageSize = 100
            };

            var result = await _apiService.PostAsync<List<ProjectFileDto>>(
                "/api/files/search", searchRequest);

            if (result != null)
            {
                Files.Clear();
                foreach (var file in result)
                {
                    Files.Add(file);
                }

                // Extract unique folder paths
                var folderPaths = result
                    .Where(f => !string.IsNullOrEmpty(f.FolderPath))
                    .Select(f => f.FolderPath!)
                    .Distinct()
                    .OrderBy(f => f)
                    .ToList();

                Folders.Clear();
                foreach (var folder in folderPaths)
                {
                    Folders.Add(folder);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load files");
            HasError = true;
            ErrorMessage = "Failed to load files. Please try again.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UploadFilesAsync()
    {
        try
        {
            var fileTypes = new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.Android, new[] { "*/*" } },
                { DevicePlatform.WinUI, new[] { "*" } },
                { DevicePlatform.Tizen, new[] { "*/*" } },
                { DevicePlatform.macOS, new[] { "*" } }
            };

            var pickOptions = new PickOptions
            {
                PickerTitle = "Select files to upload",
                FileTypes = new FilePickerFileType(fileTypes)
            };

            var results = await FilePicker.PickMultipleAsync(pickOptions);
            if (results != null && results.Any())
            {
                IsUploading = true;
                UploadProgress = 0;
                var totalFiles = results.Count();
                var uploadedFiles = 0;

                foreach (var result in results)
                {
                    try
                    {
                        UploadStatus = $"Uploading {result.FileName}... ({uploadedFiles + 1}/{totalFiles})";
                        
                        await UploadSingleFileAsync(result);
                        uploadedFiles++;
                        UploadProgress = (double)uploadedFiles / totalFiles * 100;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload file {FileName}", result.FileName);
                        await Shell.Current.DisplayAlert("Upload Error", 
                            $"Failed to upload {result.FileName}: {ex.Message}", "OK");
                    }
                }

                UploadStatus = $"Successfully uploaded {uploadedFiles} file(s)";
                await LoadFilesAsync();
                await LoadStorageStatsAsync();
                
                await Shell.Current.DisplayAlert("Upload Complete", 
                    $"Successfully uploaded {uploadedFiles} file(s)", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file upload process");
            await Shell.Current.DisplayAlert("Error", "Failed to select files for upload", "OK");
        }
        finally
        {
            IsUploading = false;
            UploadProgress = 0;
            UploadStatus = string.Empty;
        }
    }

    [RelayCommand]
    private async Task DownloadFileAsync(ProjectFileDto file)
    {
        try
        {
            IsLoading = true;
            
            // For MAUI, we'll open the file URL in the browser for download
            // In a production app, you might want to implement proper file download and save
            if (!string.IsNullOrEmpty(file.BlobUrl))
            {
                await Browser.OpenAsync(file.BlobUrl, BrowserLaunchMode.SystemPreferred);
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "File download URL not available", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file {FileId}", file.Id);
            await Shell.Current.DisplayAlert("Error", "Failed to download file", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteFileAsync(ProjectFileDto file)
    {
        try
        {
            var confirmed = await Shell.Current.DisplayAlert(
                "Delete File",
                $"Are you sure you want to delete '{file.FileName}'?",
                "Delete",
                "Cancel");

            if (confirmed)
            {
                IsLoading = true;
                await _apiService.DeleteAsync($"/api/files/{file.Id}");
                
                Files.Remove(file);
                await LoadStorageStatsAsync();
                
                await Shell.Current.DisplayAlert("Success", "File deleted successfully", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {FileId}", file.Id);
            await Shell.Current.DisplayAlert("Error", "Failed to delete file", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RenameFileAsync(ProjectFileDto file)
    {
        try
        {
            var newName = await Shell.Current.DisplayPromptAsync(
                "Rename File",
                "Enter new file name:",
                "Save",
                "Cancel",
                file.FileName);

            if (!string.IsNullOrWhiteSpace(newName) && newName != file.FileName)
            {
                IsLoading = true;
                
                var updateRequest = new FileUpdateRequest
                {
                    FileName = newName.Trim()
                };

                var updatedFile = await _apiService.PutAsync<ProjectFileDto>(
                    $"/api/files/{file.Id}", updateRequest);

                if (updatedFile != null)
                {
                    var index = Files.IndexOf(file);
                    if (index >= 0)
                    {
                        Files[index] = updatedFile;
                    }
                    
                    await Shell.Current.DisplayAlert("Success", "File renamed successfully", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename file {FileId}", file.Id);
            await Shell.Current.DisplayAlert("Error", "Failed to rename file", "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowFileDetailsAsync(ProjectFileDto file)
    {
        SelectedFile = file;
        
        var details = $"File: {file.FileName}\n" +
                     $"Size: {file.FileSizeFormatted}\n" +
                     $"Type: {file.ContentType}\n" +
                     $"Uploaded: {file.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                     $"By: {file.UploadedByName}";

        if (!string.IsNullOrEmpty(file.Description))
        {
            details += $"\n\nDescription: {file.Description}";
        }

        await Shell.Current.DisplayAlert("File Details", details, "OK");
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        try
        {
            var folderName = await Shell.Current.DisplayPromptAsync(
                "Create Folder",
                "Enter folder name:",
                "Create",
                "Cancel");

            if (!string.IsNullOrWhiteSpace(folderName))
            {
                var folderPath = string.IsNullOrEmpty(CurrentFolderPath) 
                    ? folderName.Trim()
                    : $"{CurrentFolderPath}/{folderName.Trim()}";

                // Create a placeholder file in the folder (since we can't create empty folders in blob storage)
                var placeholderRequest = new FileUploadRequest
                {
                    ProjectId = SelectedProjectId,
                    FolderPath = folderPath,
                    Description = "Folder placeholder"
                };

                // This would need a special endpoint for creating folder placeholders
                // For now, just add to folders list locally
                if (!Folders.Contains(folderPath))
                {
                    Folders.Add(folderPath);
                }

                await Shell.Current.DisplayAlert("Success", "Folder created successfully", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder");
            await Shell.Current.DisplayAlert("Error", "Failed to create folder", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToFolderAsync(string folderPath)
    {
        CurrentFolderPath = folderPath;
        await LoadFilesAsync();
    }

    [RelayCommand]
    private async Task GoUpFolderAsync()
    {
        if (!string.IsNullOrEmpty(CurrentFolderPath))
        {
            var lastSlash = CurrentFolderPath.LastIndexOf('/');
            CurrentFolderPath = lastSlash > 0 ? CurrentFolderPath.Substring(0, lastSlash) : string.Empty;
            await LoadFilesAsync();
        }
    }

    [RelayCommand]
    private async Task SearchFilesAsync()
    {
        await LoadFilesAsync();
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        ViewMode = ViewMode == "grid" ? "list" : "grid";
    }

    [RelayCommand]
    private async Task ChangeSortAsync()
    {
        var sortOptions = new[] { "CreatedAt", "FileName", "FileSize", "UploadedByName" };
        var selectedSort = await Shell.Current.DisplayActionSheet(
            "Sort by", "Cancel", null, sortOptions);

        if (!string.IsNullOrEmpty(selectedSort) && selectedSort != "Cancel")
        {
            SortBy = selectedSort;
            await LoadFilesAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleSortOrderAsync()
    {
        SortDescending = !SortDescending;
        await LoadFilesAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadFilesAsync();
        await LoadStorageStatsAsync();
    }

    [RelayCommand]
    private async Task ShowFileOptionsAsync(ProjectFileDto file)
    {
        try
        {
            var options = new[] { "Download", "Rename", "Delete", "Details" };
            var action = await Shell.Current.DisplayActionSheet(
                "File Options", "Cancel", null, options);

            switch (action)
            {
                case "Download":
                    await DownloadFileAsync(file);
                    break;
                case "Rename":
                    await RenameFileAsync(file);
                    break;
                case "Delete":
                    await DeleteFileAsync(file);
                    break;
                case "Details":
                    await ShowFileDetailsAsync(file);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing file options for file {FileId}", file.Id);
        }
    }

    [RelayCommand]
    private void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private async Task UploadSingleFileAsync(FileResult fileResult)
    {
        using var stream = await fileResult.OpenReadAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        var uploadRequest = new FileUploadRequest
        {
            ProjectId = SelectedProjectId,
            FolderPath = CurrentFolderPath,
            Description = $"Uploaded via mobile app"
        };

        // Create multipart form data
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(uploadRequest.ProjectId.ToString()), "ProjectId");
        
        if (!string.IsNullOrEmpty(uploadRequest.FolderPath))
            content.Add(new StringContent(uploadRequest.FolderPath), "FolderPath");
        
        if (!string.IsNullOrEmpty(uploadRequest.Description))
            content.Add(new StringContent(uploadRequest.Description), "Description");

        content.Add(new StringContent(uploadRequest.GenerateThumbnail.ToString()), "GenerateThumbnail");
        content.Add(new ByteArrayContent(fileBytes), "file", fileResult.FileName);

        // Send the upload request
        var response = await _apiService.PostAsync<ProjectFileDto>("/api/files/upload", content);
        
        _logger.LogInformation("File uploaded successfully: {FileName}", fileResult.FileName);
    }

    private async Task LoadProjectInfoAsync()
    {
        try
        {
            var project = await _apiService.GetAsync<ProjectDto>($"/api/projects/{SelectedProjectId}");
            if (project != null)
            {
                ProjectName = project.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project info for {ProjectId}", SelectedProjectId);
            ProjectName = "Unknown Project";
        }
    }

    private async Task LoadStorageStatsAsync()
    {
        try
        {
            StorageStats = await _apiService.GetAsync<FileStorageStatsDto>(
                $"/api/files/projects/{SelectedProjectId}/stats");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load storage stats for project {ProjectId}", SelectedProjectId);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // Implement debounced search
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // Debounce delay
            if (SearchText == value) // Only search if text hasn't changed
            {
                await MainThread.InvokeOnMainThreadAsync(async () => await SearchFilesAsync());
            }
        });
    }
}