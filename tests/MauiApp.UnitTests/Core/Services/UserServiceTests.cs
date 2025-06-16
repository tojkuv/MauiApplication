using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MauiApp.Core.Entities;
using MauiApp.Core.Interfaces;
using MauiApp.Core.Services;
using System.Linq.Expressions;
using Xunit;

namespace MauiApp.UnitTests.Core.Services;

public class UserServiceTests
{
    private readonly Mock<IRepository<User>> _mockRepository;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly UserService _userService;
    private readonly Fixture _fixture;

    public UserServiceTests()
    {
        _mockRepository = new Mock<IRepository<User>>();
        _mockLogger = new Mock<ILogger<UserService>>();
        _userService = new UserService(_mockRepository.Object, _mockLogger.Object);
        _fixture = new Fixture();
    }

    [Fact]
    public async Task GetUserByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = 1;
        var expectedUser = _fixture.Create<User>();
        expectedUser.Id = userId;
        
        _mockRepository.Setup(r => r.GetByIdAsync(userId))
                      .ReturnsAsync(expectedUser);

        // Act
        var result = await _userService.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedUser);
        _mockRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var userId = 999;
        _mockRepository.Setup(r => r.GetByIdAsync(userId))
                      .ReturnsAsync((User?)null);

        // Act
        var result = await _userService.GetUserByIdAsync(userId);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.GetByIdAsync(userId), Times.Once);
    }

    [Theory, AutoData]
    public async Task GetUserByEmailAsync_WithValidEmail_ReturnsUser(string email)
    {
        // Arrange
        var expectedUser = _fixture.Create<User>();
        expectedUser.Email = email;
        
        var users = new List<User> { expectedUser };
        
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                      .ReturnsAsync(users);

        // Act
        var result = await _userService.GetUserByEmailAsync(email);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedUser);
        _mockRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
    }

    [Theory, AutoData]
    public async Task GetUserByEmailAsync_WithNonExistentEmail_ReturnsNull(string email)
    {
        // Arrange
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                      .ReturnsAsync(new List<User>());

        // Act
        var result = await _userService.GetUserByEmailAsync(email);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllUsers()
    {
        // Arrange
        var expectedUsers = _fixture.CreateMany<User>(3).ToList();
        
        _mockRepository.Setup(r => r.GetAllAsync())
                      .ReturnsAsync(expectedUsers);

        // Act
        var result = await _userService.GetAllUsersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(expectedUsers);
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Theory, AutoData]
    public async Task CreateUserAsync_WithUniqueEmail_CreatesUser(string name, string email)
    {
        // Arrange
        var newUser = new User { Name = name, Email = email };
        
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                      .ReturnsAsync(new List<User>());
        
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<User>()))
                      .ReturnsAsync(newUser);

        // Act
        var result = await _userService.CreateUserAsync(name, email);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Email.Should().Be(email);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
    }

    [Theory, AutoData]
    public async Task CreateUserAsync_WithDuplicateEmail_ThrowsException(string name, string email)
    {
        // Arrange
        var existingUser = _fixture.Create<User>();
        existingUser.Email = email;
        
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                      .ReturnsAsync(new List<User> { existingUser });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _userService.CreateUserAsync(name, email));
        
        exception.Message.Should().Contain($"User with email {email} already exists");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WithValidUser_UpdatesUser()
    {
        // Arrange
        var user = _fixture.Create<User>();
        var originalUpdatedAt = user.UpdatedAt;
        
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<User>()))
                      .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.UpdateUserAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.UpdatedAt.Should().NotBe(originalUpdatedAt);
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        _mockRepository.Verify(r => r.UpdateAsync(user), Times.Once);
    }

    [Theory, AutoData]
    public async Task DeleteUserAsync_WithValidId_DeletesUser(int userId)
    {
        // Arrange
        _mockRepository.Setup(r => r.DeleteAsync(userId))
                      .Returns(Task.CompletedTask);

        // Act
        await _userService.DeleteUserAsync(userId);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(userId), Times.Once);
    }

    [Theory, AutoData]
    public async Task IsEmailUniqueAsync_WithUniqueEmail_ReturnsTrue(string email)
    {
        // Arrange
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                      .ReturnsAsync(new List<User>());

        // Act
        var result = await _userService.IsEmailUniqueAsync(email);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
    }

    [Theory, AutoData]
    public async Task IsEmailUniqueAsync_WithExistingEmail_ReturnsFalse(string email)
    {
        // Arrange
        var existingUser = _fixture.Create<User>();
        existingUser.Email = email;
        
        _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()))
                      .ReturnsAsync(new List<User> { existingUser });

        // Act
        var result = await _userService.IsEmailUniqueAsync(email);

        // Assert
        result.Should().BeFalse();
        _mockRepository.Verify(r => r.FindAsync(It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
    }
}