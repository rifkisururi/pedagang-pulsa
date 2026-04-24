using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Abstractions.Caching;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Configuration;
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

public class TransactionControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<TransactionController>> _loggerMock;
    private readonly Mock<IRedisService> _redisServiceMock;
    private readonly AuthService _authService;
    private readonly TransactionController _controller;

    public TransactionControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _loggerMock = MockServices.CreateLogger<TransactionController>();
        _redisServiceMock = new Mock<IRedisService>();

        _authService = new AuthService(
            _context,
            MockServices.CreateLogger<AuthService>().Object,
            _redisServiceMock.Object);

        _controller = new TransactionController(
            _context,
            _loggerMock.Object,
            _authService,
            Options.Create(new PricingConfig())
        );
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

    private string SetupValidPinSession(Guid userId)
    {
        var token = Guid.NewGuid().ToString();
        _redisServiceMock.Setup(r => r.GetAndRemoveAsync(It.Is<string>(k => k.StartsWith("pin_session_version:"))))
            .ReturnsAsync(token);
        _redisServiceMock.Setup(r => r.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return token;
    }

    [Fact]
    public async Task CreateTransaction_WithValidData_ReturnsCreated()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);
        var pinSessionToken = SetupValidPinSession(user1.Id);

        var product = await _context.Products.FirstAsync();
        var referenceId = Guid.NewGuid().ToString();

        var request = new CreateTransactionRequest
        {
            ProductId = product.Id,
            DestinationNumber = "08123456789",
            PinSessionToken = pinSessionToken
        };

        _controller.ControllerContext.HttpContext.Request.Headers["X-Reference-Id"] = referenceId;

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<TransactionResponse>().Subject;

        response.Success.Should().BeTrue();
        response.ReferenceId.Should().NotBeEmpty();
        response.Status.Should().Be("pending");
        response.Destination.Should().Be(request.DestinationNumber);
    }

    [Fact]
    public async Task CreateTransaction_WithoutReferenceId_ReturnsBadRequest()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        var request = new CreateTransactionRequest
        {
            ProductId = product.Id,
            DestinationNumber = "08123456789",
            PinSessionToken = Guid.NewGuid().ToString()
        };

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("REFERENCE_ID_REQUIRED");
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidPinSession_ReturnsUnauthorized()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        var request = new CreateTransactionRequest
        {
            ProductId = product.Id,
            DestinationNumber = "08123456789",
            PinSessionToken = "invalid-token"
        };

        // No valid PIN session set up
        _redisServiceMock.Setup(r => r.GetAndRemoveAsync(It.Is<string>(k => k.StartsWith("pin_session_version:"))))
            .ReturnsAsync((string?)null);

        _controller.ControllerContext.HttpContext.Request.Headers["X-Reference-Id"] = Guid.NewGuid().ToString();

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_PIN_SESSION");
    }

    [Fact]
    public async Task CreateTransaction_WithSupersededPinSession_ReturnsUnauthorized()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        var request = new CreateTransactionRequest
        {
            ProductId = product.Id,
            DestinationNumber = "08123456789",
            PinSessionToken = "old-token"
        };

        // PIN session version has a different token (superseded)
        _redisServiceMock.Setup(r => r.GetAndRemoveAsync(It.Is<string>(k => k.StartsWith("pin_session_version:"))))
            .ReturnsAsync("new-different-token");

        _controller.ControllerContext.HttpContext.Request.Headers["X-Reference-Id"] = Guid.NewGuid().ToString();

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorizedResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_PIN_SESSION");
        error.Message.Should().Contain("superseded");
    }

    [Fact]
    public async Task CreateTransaction_WithInsufficientBalance_ReturnsBadRequest()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);
        var pinSessionToken = SetupValidPinSession(user1.Id);

        // Set balance to 0
        var balance = await _context.UserBalances.FirstAsync(b => b.UserId == user1.Id);
        balance.ActiveBalance = 0;
        await _context.SaveChangesAsync();

        var product = await _context.Products.FirstAsync();

        var request = new CreateTransactionRequest
        {
            ProductId = product.Id,
            DestinationNumber = "08123456789",
            PinSessionToken = pinSessionToken
        };

        _controller.ControllerContext.HttpContext.Request.Headers["X-Reference-Id"] = Guid.NewGuid().ToString();

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INSUFFICIENT_BALANCE");
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidProduct_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);
        var pinSessionToken = SetupValidPinSession(user1.Id);

        var request = new CreateTransactionRequest
        {
            ProductId = Guid.NewGuid(),
            DestinationNumber = "08123456789",
            PinSessionToken = pinSessionToken
        };

        _controller.ControllerContext.HttpContext.Request.Headers["X-Reference-Id"] = Guid.NewGuid().ToString();

        // Act
        var result = await _controller.CreateTransaction(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task CreateTransaction_WithIdempotencyKey_ReturnsCachedResponse()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);
        var pinSessionToken = SetupValidPinSession(user1.Id);

        var product = await _context.Products.FirstAsync();
        var referenceId = "test-ref-id-123";

        var request = new CreateTransactionRequest
        {
            ProductId = product.Id,
            DestinationNumber = "08123456789",
            PinSessionToken = pinSessionToken
        };

        _controller.ControllerContext.HttpContext.Request.Headers["X-Reference-Id"] = referenceId;

        // First call
        await _controller.CreateTransaction(request);

        // Second call with same reference ID (idempotency should short-circuit before PIN check)
        var result = await _controller.CreateTransaction(request);

        // Assert - Should return cached response
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTransaction_WithValidReferenceId_ReturnsTransaction()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        var product = await _context.Products.FirstAsync();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = "TEST123",
            UserId = user1.Id,
            ProductId = product.Id,
            Destination = "08123456789",
            SellPrice = 5500,
            Status = TransactionStatus.Success,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetTransaction("TEST123");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
            System.Text.Json.JsonSerializer.Serialize(okResult.Value));

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("data").GetProperty("ReferenceId").GetString().Should().Be("TEST123");
    }

    [Fact]
    public async Task GetTransaction_WithInvalidReferenceId_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetTransaction("INVALID123");

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("TRANSACTION_NOT_FOUND");
    }

    [Fact]
    public async Task GetTransactions_WithNoFilter_ReturnsAllUserTransactions()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Add test transactions
        var product = await _context.Products.FirstAsync();

        var transaction1 = new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = "REF1",
            UserId = user1.Id,
            ProductId = product.Id,
            Destination = "08123456789",
            SellPrice = 5500,
            Status = TransactionStatus.Success,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var transaction2 = new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = "REF2",
            UserId = user1.Id,
            ProductId = product.Id,
            Destination = "08123456790",
            SellPrice = 10500,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.AddRange(transaction1, transaction2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetTransactions();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TransactionListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task GetTransactions_WithStatusFilter_ReturnsFilteredTransactions()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Add test transactions with different statuses
        var product = await _context.Products.FirstAsync();

        var transaction1 = new Transaction
        {
            Id = Guid.NewGuid(),
            ReferenceId = "REF3",
            UserId = user1.Id,
            ProductId = product.Id,
            Destination = "08123456789",
            SellPrice = 5500,
            Status = TransactionStatus.Success,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetTransactions(status: "success");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TransactionListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().OnlyContain(t => t.Status == "success");
    }

    [Fact]
    public async Task GetTransactions_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetTransactions(page: 1, pageSize: 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TransactionListResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CleanAsync();
        await _context.DisposeAsync();
    }
}
