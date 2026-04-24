using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
using Microsoft.Extensions.Options;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Abstractions.Sms;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Configuration;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using System.Security.Claims;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Api.Controllers;

public class AuthControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<AuthService>> _serviceLoggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly AuthService _authService;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _serviceLoggerMock = MockServices.CreateLogger<AuthService>();
        _redisServiceMock = new Mock<IRedisService>();

        _authService = new AuthService(
            _context,
            _serviceLoggerMock.Object,
            _redisServiceMock.Object,
            jwtSecret: "ThisIsASecretKeyForJWTTokenGeneration123456789",
            jwtIssuer: "PedagangPulsa",
            jwtAudience: "PedagangPulsaMobile");

        var smsClientMock = new Mock<ISmsClient>();
        var phoneVerificationService = new PhoneVerificationService(
            _context,
            _redisServiceMock.Object,
            smsClientMock.Object,
            Options.Create(new SmsGateConfig()),
            MockServices.CreateLogger<PhoneVerificationService>().Object);

        _controller = new AuthController(_authService, phoneVerificationService);
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        var request = new RegisterRequest
        {
            Username = "newuser",
            FullName = "New User",
            Email = "newuser@test.com",
            Phone = "08123456791",
            Password = "Password123!",
            Pin = "654321"
        };

        var result = await _controller.Register(request);

        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();
        response.User!.Username.Should().Be(request.Username);
        response.User.Email.Should().Be(request.Email);
        response.User.Phone.Should().Be(request.Phone);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
    {
        var request = new RegisterRequest
        {
            Username = "user1",
            FullName = "Test User",
            Email = "different@test.com",
            Phone = "08123456792",
            Password = "Password123!",
            Pin = "654321"
        };

        var result = await _controller.Register(request);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Contain("already exists");
        error.ErrorCode.Should().Be("DUPLICATE_FIELD");
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var request = new RegisterRequest
        {
            Username = "newuser2",
            FullName = "Test User",
            Email = "",
            Phone = "08123456793",
            Password = "Password123!",
            Pin = "654321"
        };

        _controller.ModelState.AddModelError("Email", "Email is required");

        var result = await _controller.Register(request);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Validation failed");
        error.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task Register_WithValidReferralCode_CreatesReferralLog()
    {
        var request = new RegisterRequest
        {
            Username = "referreduser",
            FullName = "Referred User",
            Email = "referred@test.com",
            Phone = "08123456794",
            Password = "Password123!",
            Pin = "654321",
            ReferralCode = "USER1ABC"
        };

        var result = await _controller.Register(request);

        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<RegisterResponse>().Subject;

        response.Success.Should().BeTrue();

        var referralLog = await _context.ReferralLogs
            .FirstOrDefaultAsync(rl => rl.RefereeId == response.User!.Id);

        referralLog.Should().NotBeNull();
        referralLog!.BonusStatus.Should().Be(ReferralBonusStatus.Pending);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
    {
        var request = new LoginRequest
        {
            Username = "user1",
            Password = "Password123!"
        };

        var result = await _controller.Login(request);

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
        var request = new LoginRequest
        {
            Username = "nonexistent",
            Password = "Password123!"
        };

        var result = await _controller.Login(request);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Invalid username or password");
        error.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var request = new LoginRequest
        {
            Username = "user1",
            Password = "wrongpass"
        };

        var result = await _controller.Login(request);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Invalid username or password");
        error.ErrorCode.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "valid-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var request = new RefreshTokenRequest
        {
            RefreshToken = "valid-refresh-token"
        };

        var result = await _controller.Refresh(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<RefreshTokenResponse>().Subject;

        response.Success.Should().BeTrue();
        response.AccessToken.Should().NotBeEmpty();
        response.RefreshToken.Should().NotBe("valid-refresh-token");
        response.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        var request = new RefreshTokenRequest
        {
            RefreshToken = "invalid-token"
        };

        var result = await _controller.Refresh(request);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.Message.Should().Be("Invalid or expired refresh token");
        error.ErrorCode.Should().Be("INVALID_REFRESH_TOKEN");
    }

    [Fact]
    public async Task VerifyPin_WithValidPin_ReturnsSessionToken()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var request = new VerifyPinRequest
        {
            Pin = "123456"
        };

        SetupAuthenticatedUser(user.Id);
        _redisServiceMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _redisServiceMock.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.VerifyPin(request);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<VerifyPinResponse>().Subject;

        response.Success.Should().BeTrue();
        response.PinSessionToken.Should().NotBeEmpty();
        response.ExpiresIn.Should().Be(300);

        _redisServiceMock.Verify(r => r.SetAsync(
            It.Is<string>(key => key.StartsWith($"pin_session:{user.Id}:")),
            user.Id.ToString(),
            TimeSpan.FromMinutes(5)), Times.Once);
    }

    [Fact]
    public async Task VerifyPin_WithInvalidPin_ReturnsUnauthorized()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var request = new VerifyPinRequest
        {
            Pin = "000000"
        };

        SetupAuthenticatedUser(user.Id);
        _redisServiceMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await _controller.VerifyPin(request);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_PIN");
        error.Message.Should().Contain("Invalid PIN");
    }

    [Fact]
    public async Task VerifyPin_WithLockedAccount_ReturnsLocked()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var request = new VerifyPinRequest
        {
            Pin = "123456"
        };

        SetupAuthenticatedUser(user.Id);
        _redisServiceMock.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
        _redisServiceMock.Setup(r => r.TtlAsync(It.IsAny<string>())).ReturnsAsync(900);

        var result = await _controller.VerifyPin(request);

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

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
