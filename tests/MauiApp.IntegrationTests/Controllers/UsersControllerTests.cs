using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MauiApp.ApiService.Controllers;
using MauiApp.Core.Entities;
using MauiApp.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace MauiApp.IntegrationTests.Controllers;

public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public UsersControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Get_Users_ReturnsSuccessAndCorrectContentType()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.ToString()
            .Should().Contain("application/json");
    }

    [Fact]
    public async Task Get_Users_ReturnsUsers()
    {
        // Arrange
        await SeedDatabaseAsync();

        // Act
        var response = await _client.GetAsync("/api/users");
        var users = await response.Content.ReadFromJsonAsync<List<User>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        users.Should().NotBeNull();
        users.Should().HaveCount(2); // Seeded user + test user
    }

    [Fact]
    public async Task Get_UserById_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = await SeedDatabaseAsync();

        // Act
        var response = await _client.GetAsync($"/api/users/{userId}");
        var user = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
        user.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task Get_UserById_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/users/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_User_WithValidData_CreatesUser()
    {
        // Arrange
        var createRequest = new CreateUserRequest
        {
            Name = "New User",
            Email = "newuser@example.com"
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/users", content);
        var createdUser = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        createdUser.Should().NotBeNull();
        createdUser!.Name.Should().Be("New User");
        createdUser.Email.Should().Be("newuser@example.com");
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_User_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        await SeedDatabaseAsync();
        
        var createRequest = new CreateUserRequest
        {
            Name = "Duplicate User",
            Email = "test@example.com" // Same email as seeded user
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/users", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_User_WithValidData_UpdatesUser()
    {
        // Arrange
        var userId = await SeedDatabaseAsync();
        
        var updateRequest = new UpdateUserRequest
        {
            Name = "Updated User",
            Email = "updated@example.com",
            IsActive = false
        };

        var json = JsonSerializer.Serialize(updateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PutAsync($"/api/users/{userId}", content);
        var updatedUser = await response.Content.ReadFromJsonAsync<User>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        updatedUser.Should().NotBeNull();
        updatedUser!.Name.Should().Be("Updated User");
        updatedUser.Email.Should().Be("updated@example.com");
        updatedUser.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_User_WithValidId_DeletesUser()
    {
        // Arrange
        var userId = await SeedDatabaseAsync();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/users/{userId}");
        var getResponse = await _client.GetAsync($"/api/users/{userId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_User_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/users/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<int> SeedDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Clear existing data
        context.Users.RemoveRange(context.Users);
        await context.SaveChangesAsync();
        
        // Add test user
        var user = new User
        {
            Name = "Test User",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        context.Users.Add(user);
        await context.SaveChangesAsync();
        
        return user.Id;
    }
}