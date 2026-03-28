using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using AuthService.Controllers;
using AuthService.Data;
using AuthService.Models;
using Xunit;

namespace EcommerceMicroservices.Tests;

public class AuthControllerTests
{
    private AuthDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AuthDbContext(options);
    }

    private IConfiguration CreateMockConfiguration()
    {
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Jwt:Key"]).Returns("ThisIsAVeryLongSecretKeyForJwtTokenGeneration123456!");
        return mockConfig.Object;
    }

    [Fact]
    public async Task Register_NewUser_ReturnsSuccess()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);
        
        var newUser = new User
        {
            Email = "test@example.com",
            PasswordHash = "password123"
        };

        // Act
        var result = await controller.Register(newUser);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        // Verify user was saved
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(savedUser);
        
        // Verify password was hashed (not stored in plain text)
        Assert.NotEqual("password123", savedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("password123", savedUser.PasswordHash));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);
        
        // Create existing user
        var existingUser = new User
        {
            Email = "existing@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var newUser = new User
        {
            Email = "existing@example.com", // Same email
            PasswordHash = "differentpassword"
        };

        // Act
        var result = await controller.Register(newUser);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("User already exists.", badRequestResult.Value);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);
        
        // Create user with hashed password
        var user = new User
        {
            Email = "login@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword")
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loginRequest = new User
        {
            Email = "login@example.com",
            PasswordHash = "correctpassword"
        };

        // Act
        var result = await controller.Login(loginRequest);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Verify response contains token (anonymous object with Token property)
        var responseType = okResult.Value.GetType();
        var tokenProperty = responseType.GetProperty("Token");
        Assert.NotNull(tokenProperty);
        var token = tokenProperty.GetValue(okResult.Value) as string;
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        
        // JWT tokens have 3 parts separated by dots
        var tokenParts = token.Split('.');
        Assert.Equal(3, tokenParts.Length);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);
        
        // Create user with hashed password
        var user = new User
        {
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword")
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loginRequest = new User
        {
            Email = "user@example.com",
            PasswordHash = "wrongpassword"
        };

        // Act
        var result = await controller.Login(loginRequest);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid credentials.", unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);

        var loginRequest = new User
        {
            Email = "nonexistent@example.com",
            PasswordHash = "anypassword"
        };

        // Act
        var result = await controller.Login(loginRequest);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid credentials.", unauthorizedResult.Value);
    }

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);
        
        // Create users
        var users = new[]
        {
            new User { Email = "user1@example.com", PasswordHash = "hash1" },
            new User { Email = "user2@example.com", PasswordHash = "hash2" }
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        // Act
        var result = await controller.GetUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        // Note: Returns anonymous objects with Id and Email only
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Register_PasswordIsProperlyHashed()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var config = CreateMockConfiguration();
        var controller = new AuthController(context, config);
        
        var newUser = new User
        {
            Email = "hash@example.com",
            PasswordHash = "mysecretpassword"
        };

        // Act
        var result = await controller.Register(newUser);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        
        var savedUser = await context.Users.FirstAsync(u => u.Email == "hash@example.com");
        
        // Verify it's a valid BCrypt hash (starts with $2)
        Assert.StartsWith("$2", savedUser.PasswordHash);
        
        // Verify we can verify the original password
        Assert.True(BCrypt.Net.BCrypt.Verify("mysecretpassword", savedUser.PasswordHash));
        
        // Verify wrong password fails
        Assert.False(BCrypt.Net.BCrypt.Verify("wrongpassword", savedUser.PasswordHash));
    }
}
