using Microsoft.Extensions.Logging;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;

namespace MauiApp.Core.Services;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IRepository<User> userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        _logger.LogInformation("Getting user by ID: {UserId}", id);
        return await _userRepository.GetByIdAsync(id);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        _logger.LogInformation("Getting user by email: {Email}", email);
        var users = await _userRepository.FindAsync(u => u.Email == email);
        return users.FirstOrDefault();
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        _logger.LogInformation("Getting all users");
        return await _userRepository.GetAllAsync();
    }

    public async Task<User> CreateUserAsync(string name, string email)
    {
        _logger.LogInformation("Creating user with email: {Email}", email);
        
        if (!await IsEmailUniqueAsync(email))
        {
            throw new InvalidOperationException($"User with email {email} already exists");
        }

        var user = new User
        {
            Name = name,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        return await _userRepository.AddAsync(user);
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        _logger.LogInformation("Updating user: {UserId}", user.Id);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        return user;
    }

    public async Task DeleteUserAsync(int id)
    {
        _logger.LogInformation("Deleting user: {UserId}", id);
        await _userRepository.DeleteAsync(id);
    }

    public async Task<bool> IsEmailUniqueAsync(string email)
    {
        var users = await _userRepository.FindAsync(u => u.Email == email);
        return !users.Any();
    }
}