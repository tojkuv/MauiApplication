using AutoFixture;
using MauiApp.Core.Entities;

namespace MauiApp.UnitTests.Helpers;

public static class TestDataFactory
{
    private static readonly Fixture _fixture = new();

    static TestDataFactory()
    {
        ConfigureFixture();
    }

    private static void ConfigureFixture()
    {
        // Configure AutoFixture to generate valid data
        _fixture.Customize<User>(composer => composer
            .With(u => u.Email, () => _fixture.Create<string>() + "@example.com")
            .With(u => u.Name, () => _fixture.Create<string>())
            .With(u => u.CreatedAt, () => DateTime.UtcNow.AddDays(-_fixture.Create<int>() % 365))
            .With(u => u.IsActive, true));
    }

    public static User CreateUser(string? email = null, string? name = null)
    {
        var user = _fixture.Create<User>();
        
        if (!string.IsNullOrEmpty(email))
            user.Email = email;
            
        if (!string.IsNullOrEmpty(name))
            user.Name = name;
            
        return user;
    }

    public static List<User> CreateUsers(int count)
    {
        return _fixture.CreateMany<User>(count).ToList();
    }

    public static User CreateValidUser()
    {
        return new User
        {
            Id = _fixture.Create<int>(),
            Name = "Test User",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static User CreateInactiveUser()
    {
        var user = CreateValidUser();
        user.IsActive = false;
        return user;
    }

    public static User CreateUserWithoutId()
    {
        var user = CreateValidUser();
        user.Id = 0;
        return user;
    }
}