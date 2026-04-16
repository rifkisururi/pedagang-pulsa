using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Application.Services;

public class AuthServiceTests : IAsyncLifetime
{
    private TestDbContext _context = null!;
    private AuthService _authService = null!;
    private Mock<ILogger<AuthService>> _loggerMock = null!;

    public async Task InitializeAsync()
    {
        _context = new TestDbContext();

        // Clean up database before seeding to avoid primary key conflicts
        await _context.CleanupBeforeSeedAsync();

        await _context.SeedAsync();

        _loggerMock = MockServices.CreateLogger<AuthService>();
        _authService = new AuthService(_context, _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        // Clean up test data after all tests
        await _context.CleanupBeforeSeedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task RegisterAsync_ValidData_ReturnsUser()
    {
        // Arrange
        var username = "newuser";
        var fullName = "New User";
        var email = "newuser@test.com";
        var phone = "08129876543";
        var pin = "123456";

        // Act
        var result = await _authService.RegisterAsync(username, fullName, email, phone, pin);

        // Assert
        result.User.Should().NotBeNull();
        result.ErrorMessage.Should().BeEmpty();
        result.User!.Username.Should().Be(username);
        result.User.Email.Should().Be(email);
        result.User.Phone.Should().Be(phone);
        result.User.Status.Should().Be(UserStatus.Active);
        result.User.PinFailedAttempts.Should().Be(0);
        result.User.PinLockedAt.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsername_ReturnsError()
    {
        // Arrange
        var username = "user1"; // Already exists in seeded data

        // Act
        var result = await _authService.RegisterAsync(username, "Test", "test@test.com", "08123456789", "123456");

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Username already exists");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsError()
    {
        // Arrange
        var email = "user1@test.com"; // Already exists in seeded data

        // Act
        var result = await _authService.RegisterAsync("newuser", "Test", email, "08123456789", "123456");

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Email already exists");
    }

    [Fact]
    public async Task RegisterAsync_DuplicatePhone_ReturnsError()
    {
        // Arrange
        var phone = "08123456789"; // Already exists in seeded data

        // Act
        var result = await _authService.RegisterAsync("newuser", "Test", "new@test.com", phone, "123456");

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Phone number already exists");
    }

    [Fact]
    public async Task RegisterAsync_WithValidReferral_CreatesReferralLog()
    {
        // Arrange
        var referralCode = "USER1ABC"; // user1's referral code

        // Act
        var result = await _authService.RegisterAsync("newuser", "Test", "new@test.com", "08129998888", "123456", referralCode);

        // Assert
        result.User.Should().NotBeNull();
        result.ErrorMessage.Should().BeEmpty();
        result.User!.ReferredBy.Should().Be(_context.Users.First(u => u.ReferralCode == referralCode).Id);

        var referralLog = await _context.ReferralLogs
            .Where(rl => rl.RefereeId == result.User.Id)
            .FirstOrDefaultAsync();
        referralLog.Should().NotBeNull();
        referralLog!.BonusStatus.Should().Be(ReferralBonusStatus.Pending);
    }

    [Fact]
    public async Task RegisterAsync_WithInvalidReferral_ReturnsError()
    {
        // Arrange
        var invalidReferralCode = "INVALID123";

        // Act
        var result = await _authService.RegisterAsync("newuser", "Test", "new@test.com", "08129998888", "123456", invalidReferralCode);

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Invalid referral code");
    }

    [Fact]
    public async Task RegisterAsync_VerifiesPinHashing()
    {
        // Arrange
        var pin = "123456";

        // Act
        var result = await _authService.RegisterAsync("newuser", "Test", "new@test.com", "08129998888", pin);

        // Assert
        result.User.Should().NotBeNull();
        result.User!.PinHash.Should().NotBe(pin);
        result.User.PinHash.Should().NotBeNullOrEmpty();

        // Verify the hash is valid BCrypt hash
        bool isValid = BCrypt.Net.BCrypt.Verify(pin, result.User.PinHash);
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        // Arrange
        var username = "user1";
        var pin = "123456";

        // Act
        var result = await _authService.LoginAsync(username, pin);

        // Assert
        result.User.Should().NotBeNull();
        result.ErrorMessage.Should().BeEmpty();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.User!.Username.Should().Be(username);
    }

    [Fact]
    public async Task LoginAsync_WithWrongUsername_ReturnsError()
    {
        // Arrange
        var username = "nonexistent";
        var pin = "123456";

        // Act
        var result = await _authService.LoginAsync(username, pin);

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Invalid username or PIN");
        result.AccessToken.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_WithWrongPin_ReturnsError()
    {
        // Arrange
        var username = "user1";
        var pin = "wrongpin";

        // Act
        var result = await _authService.LoginAsync(username, pin);

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Contain("Invalid PIN");
        result.AccessToken.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
    }

    [Fact]
    public async Task LoginAsync_WithSuspendedAccount_ReturnsError()
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");
        user.Status = UserStatus.Suspended;
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.LoginAsync("user1", "123456");

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Account is suspended");

        // Cleanup
        user.Status = UserStatus.Active;
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ReturnsError()
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");
        user.Status = UserStatus.Inactive;
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.LoginAsync("user1", "123456");

        // Assert
        result.User.Should().BeNull();
        result.ErrorMessage.Should().Be("Account is inactive");

        // Cleanup
        user.Status = UserStatus.Active;
        await _context.SaveChangesAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task VerifyPinAsync_WithCorrectPin_ResetsFailedAttempts(int failedAttempts)
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");
        user.PinFailedAttempts = (short)failedAttempts;
        // Don't set PinLockedAt - we want to test that correct PIN resets failed attempts
        // even when there are failed attempts (but not locked yet)
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.VerifyPinAsync(user.Id, "123456");

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeEmpty();
        result.PinSessionToken.Should().NotBeNullOrEmpty();

        // Refresh user from database
        _context.Entry(user).ReloadAsync().Wait();
        user.PinFailedAttempts.Should().Be(0);
        user.PinLockedAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyPinAsync_WithWrongPin_FirstAttempt_ReturnsTwoAttemptsLeft()
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");

        // Act
        var result = await _authService.VerifyPinAsync(user.Id, "wrongpin");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid PIN. 2 attempts remaining");

        _context.Entry(user).ReloadAsync().Wait();
        user.PinFailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task VerifyPinAsync_WithWrongPin_SecondAttempt_ReturnsOneAttemptLeft()
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");
        user.PinFailedAttempts = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.VerifyPinAsync(user.Id, "wrongpin");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Invalid PIN. 1 attempts remaining");

        _context.Entry(user).ReloadAsync().Wait();
        user.PinFailedAttempts.Should().Be(2);
    }

    [Fact]
    public async Task VerifyPinAsync_WithWrongPin_ThirdAttempt_LocksAccount()
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");
        user.PinFailedAttempts = 2;
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.VerifyPinAsync(user.Id, "wrongpin");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Account locked for 15 minutes due to too many failed attempts");

        _context.Entry(user).ReloadAsync().Wait();
        user.PinFailedAttempts.Should().Be(3);
        user.PinLockedAt.Should().NotBeNull();
        user.PinLockedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task VerifyPinAsync_WithLockedAccount_ReturnsLockoutMessage()
    {
        // Arrange
        var user = _context.Users.First(u => u.Username == "user1");
        user.PinFailedAttempts = 3;
        user.PinLockedAt = DateTime.UtcNow.AddMinutes(-5); // Locked 5 minutes ago
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.VerifyPinAsync(user.Id, "123456");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Account is locked");
        result.ErrorMessage.Should().Contain("minutes");

        // Cleanup
        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_GeneratesNewTokens()
    {
        // Arrange
        var loginResult = await _authService.LoginAsync("user1", "123456");
        var oldRefreshToken = loginResult.RefreshToken;

        // Act
        var result = await _authService.RefreshTokenAsync(oldRefreshToken);

        // Assert
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe(oldRefreshToken);
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshTokenAsync_WithInvalidToken_ReturnsError()
    {
        // Arrange
        var invalidToken = "invalid_token";

        // Act
        var result = await _authService.RefreshTokenAsync(invalidToken);

        // Assert
        result.AccessToken.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithRevokedToken_ReturnsError()
    {
        // Arrange
        var loginResult = await _authService.LoginAsync("user1", "123456");
        var refreshToken = await _context.RefreshTokens
            .Where(t => t.Token == loginResult.RefreshToken)
            .FirstOrDefaultAsync();
        refreshToken!.IsRevoked = true;
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.RefreshTokenAsync(loginResult.RefreshToken);

        // Assert
        result.AccessToken.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Refresh token has been revoked");
    }

    [Fact]
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
