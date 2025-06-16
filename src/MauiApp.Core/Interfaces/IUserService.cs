using MauiApp.Core.Entities;

namespace MauiApp.Core.Interfaces;

public interface IUserService
{
    Task<User?> GetUserByIdAsync(int id);
    Task<User?> GetUserByEmailAsync(string email);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> CreateUserAsync(string name, string email);
    Task<User> UpdateUserAsync(User user);
    Task DeleteUserAsync(int id);
    Task<bool> IsEmailUniqueAsync(string email);
}