using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Api.Controllers;

public class TransferControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<TransferController>> _loggerMock;
    private readonly TransferController _controller;

    public TransferControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _loggerMock = MockServices.CreateLogger<TransferController>();
        _controller = new TransferController(_context, _loggerMock.Object);
    }

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task Transfer_WithValidData_ReturnsOk()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        var user2 = await _context.Users.FirstAsync(u => u.UserName == "user2");

        // Setup user2 with transfer capability
        var level2 = await _context.UserLevels.FirstAsync(l => l.Name == "Member2");
        user1.LevelId = level2.Id;
        await _context.SaveChangesAsync();

        // Add level config to allow transfer
        var config = new Domain.Entities.UserLevelConfig
        {
            LevelId = level2.Id,
            ConfigKey = "can_transfer",
            ConfigValue = "true"
        };
        _context.UserLevelConfigs.Add(config);
        await _context.SaveChangesAsync();

        SetupAuthenticatedUser(user1.Id);

        var initialBalance = user1.Balance!.ActiveBalance;

        var request = new TransferRequestDto
        {
            ToUsername = "user2",
            Amount = 50000,
            Notes = "Test transfer"
        };

        // Act
        var result = await _controller.Transfer(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.data.amount.Should().Be(50000);

        // Verify balance was updated
        await _context.Entry(user1.Balance).ReloadAsync();
        user1.Balance.ActiveBalance.Should().Be(initialBalance - 50000);
    }

    [Fact]
    public async Task Transfer_WithInvalidRecipient_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var request = new TransferRequestDto
        {
            ToUsername = "nonexistent",
            Amount = 50000
        };

        // Act
        var result = await _controller.Transfer(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("RECIPIENT_NOT_FOUND");
    }

    [Fact]
    public async Task Transfer_ToSelf_ReturnsBadRequest()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var request = new TransferRequestDto
        {
            ToUsername = "user1",
            Amount = 50000
        };

        // Act
        var result = await _controller.Transfer(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_TRANSFER");
    }

    [Fact]
    public async Task Transfer_WithInsufficientBalance_ReturnsBadRequest()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var request = new TransferRequestDto
        {
            ToUsername = "user2",
            Amount = 999999999 // More than balance
        };

        // Act
        var result = await _controller.Transfer(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INSUFFICIENT_BALANCE");
    }

    [Fact]
    public async Task Transfer_WithInvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var request = new TransferRequestDto
        {
            ToUsername = "user2",
            Amount = -100 // Negative amount
        };

        // Act
        var result = await _controller.Transfer(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_AMOUNT");
    }

    [Fact]
    public async Task GetTransferHistory_WithAuthenticatedUser_ReturnsHistory()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var level2 = await _context.UserLevels.FirstAsync(l => l.Name == "Member2");
        user1.LevelId = level2.Id;
        await _context.SaveChangesAsync();

        var config = new Domain.Entities.UserLevelConfig
        {
            LevelId = level2.Id,
            ConfigKey = "can_transfer",
            ConfigValue = "true"
        };
        _context.UserLevelConfigs.Add(config);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetTransferHistory();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.totalRecords.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task GetTransferHistory_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetTransferHistory(page: 1, pageSize: 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.page.Should().Be(1);
        response.pageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetTransferHistory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authenticated user

        // Act
        var result = await _controller.GetTransferHistory();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_TOKEN");
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CleanAsync();
        await _context.DisposeAsync();
    }
}
