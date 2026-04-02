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

public class BalanceControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<BalanceController>> _loggerMock;
    private readonly BalanceController _controller;

    public BalanceControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _loggerMock = MockServices.CreateLogger<BalanceController>();
        _controller = new BalanceController(_context, _loggerMock.Object);
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
    public async Task GetBalance_WithAuthenticatedUser_ReturnsBalance()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetBalance();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BalanceResponse>().Subject;

        response.Success.Should().BeTrue();
        response.ActiveBalance.Should().BeGreaterThan(0);
        response.TotalBalance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBalance_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authenticated user

        // Act
        var result = await _controller.GetBalance();

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task GetHistory_WithAuthenticatedUser_ReturnsHistory()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Add some ledger entries
        var ledger = new Domain.Entities.BalanceLedger
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            UserId = user1.Id,
            Type = Domain.Enums.BalanceTransactionType.Topup,
            Amount = 100000,
            ActiveBefore = 900000,
            ActiveAfter = 1000000,
            HeldBefore = 0,
            HeldAfter = 0,
            Notes = "Test top-up",
            CreatedAt = DateTime.UtcNow
        };

        _context.BalanceLedgers.Add(ledger);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetHistory();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BalanceHistoryResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.TotalRecords.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetHistory_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetHistory(page: 1, pageSize: 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<BalanceHistoryResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetHistory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No authenticated user

        // Act
        var result = await _controller.GetHistory();

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
