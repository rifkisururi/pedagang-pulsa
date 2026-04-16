using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Api.Controllers;

public class AuthControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _configurationMock = new Mock<IConfiguration>();
        SetupConfiguration();

        _loggerMock = MockServices.CreateLogger<AuthController>();
        _redisServiceMock = new Mock<IRedisService>();

        _controller = new AuthController(
            _configurationMock.Object,
            _context,
            _loggerMock.Object,
            _redisServiceMock.Object
        );
    }

    private void SetupConfiguration()
    {
        _configurationMock.Setup(c => c["Jwt:Key"]).Returns("FallbackLocalSecretKeyForJWTTokenGeneration123456789");
        _configurationMock.Setup(c => c["Jwt:Issuer"]).Returns("PedagangPulsa");
        _configurationMock.Setup(c => c["Jwt:Audience"]).Returns("PedagangPulsaMobile");
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            FullName = "New User",
            Email = "newuser@test.com",
            Phone = "08123456791",
            Pin = "654321",
            ReferralCode = null
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();
        response.User.Username.Should().Be(request.Username);
        response.User.Email.Should().Be(request.Email);
        response.User.Phone.Should().Be(request.Phone);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "user1", // Already exists
            FullName = "Test User",
            Email = "different@test.com",
            Phone = "08123456792",
            Pin = "654321"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Contain("already exists");
        error.ErrorCode.Should().Be("DUPLICATE_FIELD");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser2",
            FullName = "Test User",
            Email = "", // Invalid
            Phone = "08123456793",
            Pin = "654321"
        };

        _controller.ModelState.AddModelError("Email", "Email is required");

        // Act
        var result = await _controller.Register(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Validation failed");
        error.ErrorCode.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WithValidReferralCode_CreatesReferralLog()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "referreduser",
            FullName = "Referred User",
            Email = "referred@test.com",
            Phone = "08123456794",
            Pin = "654321",
            ReferralCode = "USER1ABC" // Valid referral code
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();

        // Verify referral was created
        var referralLog = await _context.ReferralLogs
            .FirstOrDefaultAsync(rl => rl.RefereeId.ToString() == response.User.Id.ToString());

        referralLog.Should().NotBeNull();
        referralLog.BonusStatus.Should().Be(ReferralBonusStatus.Pending);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user1",
            Pin = "123456"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<LoginResponse>().Subject;

        response.Success.Should().BeTrue();
        response.AccessToken.Should().NotBeEmpty();
        response.RefreshToken.Should().NotBeEmpty();
        response.ExpiresIn.Should().Be(900);
        response.User.Username.Should().Be(request.Username);
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "nonexistent",
            Pin = "123456"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Invalid username or PIN");
        error.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithInvalidPin_ReturnsUnauthorized()
    {
        // Arrange
        var request = new LoginRequest
        {
            Username = "user1",
            Pin = "wrongpin"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Invalid username or PIN");
        error.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.Username == "user1");
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "valid-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid-refresh-token"
        };

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RefreshTokenResponse>().Subject;

        response.Success.Should().BeTrue();
        response.AccessToken.Should().NotBeEmpty();
        response.RefreshToken.Should().NotBe("valid-refresh-token"); // Should be new token
        response.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid-token"
        };

        // Act
        var result = await _controller.Refresh(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Invalid or expired refresh token");
        error.ErrorCode.Should().Be("INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task VerifyPin_WithValidPin_ReturnsSessionToken()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.Username == "user1");
        var request = new VerifyPinRequest
        {
            Pin = "123456"
        };

        // Setup controller with user context
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        _redisServiceMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _redisServiceMock.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.VerifyPin(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<VerifyPinResponse>().Subject;

        response.Success.Should().BeTrue();
        response.PinSessionToken.Should().NotBeEmpty();
        response.ExpiresIn.Should().Be(300);

        _redisServiceMock.Verify(r => r.SetAsync(
            It.IsAny<string>(),
            user.Id.ToString(),
            TimeSpan.FromMinutes(5)
        ), Times.Once);
    }

    [Fact]
    public async Task VerifyPin_WithInvalidPin_ReturnsUnauthorized()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.Username == "user1");
        var request = new VerifyPinRequest
        {
            Pin = "wrongpin"
        };

        // Setup controller with user context
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        _redisServiceMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        // Act
        var result = await _controller.VerifyPin(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_PIN");
        error.Message.Should().Contain("Invalid PIN");
    }

    [Fact]
    public async Task VerifyPin_WithLockedAccount_ReturnsLocked()
    {
        // Arrange
        var user = await _context.Users.FirstAsync(u => u.Username == "user1");
        var request = new VerifyPinRequest
        {
            Pin = "123456"
        };

        // Setup controller with user context
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        _redisServiceMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _redisServiceMock.Setup(r => r.TtlAsync(It.IsAny<string>())).ReturnsAsync(900);

        // Act
        var result = await _controller.VerifyPin(request);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(429);

        var error = statusCodeResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.ErrorCode.Should().Be("ACCOUNT_LOCKED");
        error.Message.Should().Contain("locked");
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CleanAsync();
        await _context.DisposeAsync();
    }
}
