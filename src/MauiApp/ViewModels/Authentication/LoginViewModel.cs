using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace MauiApp.ViewModels.Authentication;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmailError))]
    private string emailError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordError))]
    private string passwordError = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool rememberMe = true;

    [ObservableProperty]
    private bool isPasswordHidden = true;

    [ObservableProperty]
    private bool isBiometricAvailable;

    public bool IsNotBusy => !IsBusy;
    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);
    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);
    public string PasswordToggleIcon => IsPasswordHidden ? "👁" : "🙈";

    public LoginViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService,
        ILogger<LoginViewModel> logger)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _logger = logger;

        // Check if biometric authentication is available
        CheckBiometricAvailability();
    }

    [RelayCommand]
    private async Task Login()
    {
        if (IsBusy)
            return;

        try
        {
            ClearErrors();
            
            if (!ValidateInput())
                return;

            IsBusy = true;
            _logger.LogInformation("Attempting login for user: {Email}", Email);

            var result = await _authenticationService.LoginAsync(Email, Password);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Login successful for user: {Email}", Email);
                
                // Navigate to main page
                await _navigationService.NavigateToAsync("//MainPage");
            }
            else
            {
                _logger.LogWarning("Login failed for user {Email}: {Error}", Email, result.ErrorMessage);
                
                // Show error message
                await Shell.Current.DisplayAlert("Login Failed", 
                    result.ErrorMessage ?? "Invalid email or password. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Email}", Email);
            await Shell.Current.DisplayAlert("Error", 
                "An unexpected error occurred. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SocialLogin(string provider)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            _logger.LogInformation("Attempting social login with provider: {Provider}", provider);

            var result = await _authenticationService.LoginWithProviderAsync(provider);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Social login successful with provider: {Provider}", provider);
                await _navigationService.NavigateToAsync("//MainPage");
            }
            else
            {
                _logger.LogWarning("Social login failed with provider {Provider}: {Error}", provider, result.ErrorMessage);
                await Shell.Current.DisplayAlert("Login Failed", 
                    result.ErrorMessage ?? "Authentication failed. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during social login with provider: {Provider}", provider);
            await Shell.Current.DisplayAlert("Error", 
                "An unexpected error occurred during authentication. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BiometricLogin()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            _logger.LogInformation("Attempting biometric login");

            // Check if we have stored credentials for biometric auth
            var hasStoredCredentials = await CheckStoredCredentials();
            
            if (!hasStoredCredentials)
            {
                await Shell.Current.DisplayAlert("Biometric Login", 
                    "Please sign in with your email and password first to enable biometric authentication.", "OK");
                return;
            }

            // Perform biometric authentication
            var biometricResult = await PerformBiometricAuth();
            
            if (biometricResult)
            {
                // Get stored credentials and login
                var credentials = await GetStoredCredentials();
                if (credentials != null)
                {
                    Email = credentials.Email;
                    Password = credentials.Password;
                    await Login();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during biometric login");
            await Shell.Current.DisplayAlert("Error", 
                "Biometric authentication failed. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ForgotPassword()
    {
        try
        {
            await _navigationService.NavigateToAsync("ForgotPasswordPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to forgot password page");
            await Shell.Current.DisplayAlert("Error", "Unable to navigate to forgot password page", "OK");
        }
    }

    [RelayCommand]
    private async Task NavigateToRegister()
    {
        try
        {
            await _navigationService.NavigateToAsync("RegisterPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to register page");
            await Shell.Current.DisplayAlert("Error", "Unable to navigate to register page", "OK");
        }
    }

    [RelayCommand]
    private async Task Back()
    {
        try
        {
            await _navigationService.NavigateToAsync("WelcomePage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating back to welcome page");
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    [RelayCommand]
    private void FocusPassword()
    {
        // This would be handled in the code-behind to focus the password entry
    }

    private bool ValidateInput()
    {
        var isValid = true;

        // Validate email
        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Email is required";
            isValid = false;
        }
        else if (!IsValidEmail(Email))
        {
            EmailError = "Please enter a valid email address";
            isValid = false;
        }

        // Validate password
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required";
            isValid = false;
        }
        else if (Password.Length < 6)
        {
            PasswordError = "Password must be at least 6 characters";
            isValid = false;
        }

        return isValid;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var emailAttribute = new EmailAddressAttribute();
            return emailAttribute.IsValid(email);
        }
        catch
        {
            return false;
        }
    }

    private void ClearErrors()
    {
        EmailError = string.Empty;
        PasswordError = string.Empty;
    }

    private async void CheckBiometricAvailability()
    {
        try
        {
            // For now, disable biometric authentication until proper implementation
            IsBiometricAvailable = false;
        }
        catch
        {
            IsBiometricAvailable = false;
        }
    }

    private async Task<bool> CheckStoredCredentials()
    {
        try
        {
            // Check if we have stored credentials for biometric auth
            var hasCredentials = await SecureStorage.GetAsync("BiometricEnabled");
            return !string.IsNullOrEmpty(hasCredentials);
        }
        catch
        {
            return false;
        }
    }

    private async Task<(string Email, string Password)?> GetStoredCredentials()
    {
        try
        {
            var email = await SecureStorage.GetAsync("StoredEmail");
            var password = await SecureStorage.GetAsync("StoredPassword");
            
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                return (email, password);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stored credentials");
        }
        
        return null;
    }

    private async Task<bool> PerformBiometricAuth()
    {
        try
        {
            // Placeholder for biometric authentication
            // This would be implemented using platform-specific APIs
            await Task.Delay(1000); // Simulate authentication delay
            return false; // For now, always return false
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Biometric authentication failed");
            return false;
        }
    }
}