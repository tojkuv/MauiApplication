using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace MauiApp.ViewModels.Authentication;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<RegisterViewModel> _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFirstNameError))]
    private string firstNameError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastNameError))]
    private string lastNameError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmailError))]
    private string emailError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasswordError), nameof(MinLengthCheckIcon), nameof(MinLengthCheckColor), 
                              nameof(UpperCaseCheckIcon), nameof(UpperCaseCheckColor), nameof(NumberCheckIcon), nameof(NumberCheckColor))]
    private string passwordError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConfirmPasswordError))]
    private string confirmPasswordError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTermsError))]
    private string termsError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinLengthCheckIcon), nameof(MinLengthCheckColor), 
                              nameof(UpperCaseCheckIcon), nameof(UpperCaseCheckColor), 
                              nameof(NumberCheckIcon), nameof(NumberCheckColor))]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinLengthCheckIcon), nameof(MinLengthCheckColor), 
                              nameof(UpperCaseCheckIcon), nameof(UpperCaseCheckColor), 
                              nameof(NumberCheckIcon), nameof(NumberCheckColor))]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool acceptTerms;

    [ObservableProperty]
    private bool isPasswordHidden = true;

    public bool IsNotBusy => !IsBusy;
    public bool HasFirstNameError => !string.IsNullOrEmpty(FirstNameError);
    public bool HasLastNameError => !string.IsNullOrEmpty(LastNameError);
    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);
    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);
    public bool HasConfirmPasswordError => !string.IsNullOrEmpty(ConfirmPasswordError);
    public bool HasTermsError => !string.IsNullOrEmpty(TermsError);
    public string PasswordToggleIcon => IsPasswordHidden ? "👁" : "🙈";

    // Password validation indicators
    public string MinLengthCheckIcon => Password.Length >= 8 ? "✅" : "❌";
    public Color MinLengthCheckColor => Password.Length >= 8 ? Colors.Green : Colors.Red;
    
    public string UpperCaseCheckIcon => Regex.IsMatch(Password, @"[A-Z]") ? "✅" : "❌";
    public Color UpperCaseCheckColor => Regex.IsMatch(Password, @"[A-Z]") ? Colors.Green : Colors.Red;
    
    public string NumberCheckIcon => Regex.IsMatch(Password, @"[0-9]") ? "✅" : "❌";
    public Color NumberCheckColor => Regex.IsMatch(Password, @"[0-9]") ? Colors.Green : Colors.Red;

    public RegisterViewModel(
        IAuthenticationService authenticationService,
        INavigationService navigationService,
        ILogger<RegisterViewModel> logger)
    {
        _authenticationService = authenticationService;
        _navigationService = navigationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task Register()
    {
        if (IsBusy)
            return;

        try
        {
            ClearErrors();
            
            if (!ValidateInput())
                return;

            IsBusy = true;
            _logger.LogInformation("Attempting registration for user: {Email}", Email);

            var result = await _authenticationService.RegisterAsync(Email, Password, FirstName, LastName);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Registration successful for user: {Email}", Email);
                
                // Show success message
                await Shell.Current.DisplayAlert("Registration Successful", 
                    "Your account has been created successfully! Welcome to Project Hub.", "Continue");
                
                // Navigate to main page
                await _navigationService.NavigateToAsync("//MainPage");
            }
            else
            {
                _logger.LogWarning("Registration failed for user {Email}: {Error}", Email, result.ErrorMessage);
                
                // Show error message
                await Shell.Current.DisplayAlert("Registration Failed", 
                    result.ErrorMessage ?? "Registration failed. Please try again.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user: {Email}", Email);
            await Shell.Current.DisplayAlert("Error", 
                "An unexpected error occurred. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToLogin()
    {
        try
        {
            await _navigationService.NavigateToAsync("LoginPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to login page");
            await Shell.Current.DisplayAlert("Error", "Unable to navigate to login page", "OK");
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
    private void FocusLastName()
    {
        // This would be handled in the code-behind to focus the last name entry
    }

    [RelayCommand]
    private void FocusEmail()
    {
        // This would be handled in the code-behind to focus the email entry
    }

    [RelayCommand]
    private void FocusPassword()
    {
        // This would be handled in the code-behind to focus the password entry
    }

    [RelayCommand]
    private void FocusConfirmPassword()
    {
        // This would be handled in the code-behind to focus the confirm password entry
    }

    [RelayCommand]
    private async Task OpenTerms()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://yourcompany.com/terms"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening terms of service");
            await Shell.Current.DisplayAlert("Error", "Unable to open Terms of Service", "OK");
        }
    }

    [RelayCommand]
    private async Task OpenPrivacy()
    {
        try
        {
            await Launcher.OpenAsync(new Uri("https://yourcompany.com/privacy"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening privacy policy");
            await Shell.Current.DisplayAlert("Error", "Unable to open Privacy Policy", "OK");
        }
    }

    private bool ValidateInput()
    {
        var isValid = true;

        // Validate first name
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            FirstNameError = "First name is required";
            isValid = false;
        }
        else if (FirstName.Length < 2)
        {
            FirstNameError = "First name must be at least 2 characters";
            isValid = false;
        }

        // Validate last name
        if (string.IsNullOrWhiteSpace(LastName))
        {
            LastNameError = "Last name is required";
            isValid = false;
        }
        else if (LastName.Length < 2)
        {
            LastNameError = "Last name must be at least 2 characters";
            isValid = false;
        }

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
        else if (!IsValidPassword(Password))
        {
            PasswordError = "Password must meet all requirements";
            isValid = false;
        }

        // Validate confirm password
        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            ConfirmPasswordError = "Please confirm your password";
            isValid = false;
        }
        else if (Password != ConfirmPassword)
        {
            ConfirmPasswordError = "Passwords do not match";
            isValid = false;
        }

        // Validate terms acceptance
        if (!AcceptTerms)
        {
            TermsError = "You must accept the Terms of Service and Privacy Policy";
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

    private static bool IsValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return false;

        // Check for at least one uppercase letter
        if (!Regex.IsMatch(password, @"[A-Z]"))
            return false;

        // Check for at least one number
        if (!Regex.IsMatch(password, @"[0-9]"))
            return false;

        return true;
    }

    private void ClearErrors()
    {
        FirstNameError = string.Empty;
        LastNameError = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        TermsError = string.Empty;
    }
}