using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting;
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
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Api.Controllers;

public class TopupControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly Mock<ILogger<TopupController>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly TopupController _controller;

    public TopupControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _loggerMock = MockServices.CreateLogger<TopupController>();
        _configurationMock = new Mock<IConfiguration>();
        _environmentMock = new Mock<IWebHostEnvironment>();

        _environmentMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

        _controller = new TopupController(
            _context,
            _loggerMock.Object,
            _configurationMock.Object,
            _environmentMock.Object
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

    private Mock<IFormFile> CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), System.Threading.CancellationToken.None))
            .Callback<Stream, System.Threading.CancellationToken>((s, ct) =>
            {
                s.Write(content, 0, content.Length);
            })
            .Returns(Task.CompletedTask);

        return mockFile;
    }

    [Fact]
    public async Task CreateTopupRequest_WithValidData_ReturnsCreated()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Add a bank account
        var bankAccount = new BankAccount
        {
            BankName = "BCA",
            AccountNumber = "1234567890",
            AccountName = "Test Account"
        };
        _context.BankAccounts.Add(bankAccount);
        await _context.SaveChangesAsync();

        var fileContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var mockFile = CreateMockFile("test.jpg", "image/jpeg", fileContent);

        var request = new CreateTopupRequestDto
        {
            BankAccountId = bankAccount.Id,
            Amount = 100000,
            Notes = "Test topup",
            TransferProof = mockFile.Object
        };

        // Act
        var result = await _controller.CreateTopupRequest(request);

        // Assert
        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var response = createdResult.Value.Should().BeOfType<TopupResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Status.Should().Be("pending");
        response.Amount.Should().Be(100000);
    }

    [Fact]
    public async Task CreateTopupRequest_WithInvalidFileType_ReturnsBadRequest()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var bankAccount = new BankAccount
        {
            BankName = "BCA",
            AccountNumber = "1234567890",
            AccountName = "Test Account"
        };
        _context.BankAccounts.Add(bankAccount);
        await _context.SaveChangesAsync();

        var fileContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var mockFile = CreateMockFile("test.exe", "application/exe", fileContent);

        var request = new CreateTopupRequestDto
        {
            BankAccountId = bankAccount.Id,
            Amount = 100000,
            TransferProof = mockFile.Object
        };

        // Act
        var result = await _controller.CreateTopupRequest(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("INVALID_FILE_TYPE");
    }

    [Fact]
    public async Task CreateTopupRequest_WithInvalidBankAccount_ReturnsNotFound()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var fileContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var mockFile = CreateMockFile("test.jpg", "image/jpeg", fileContent);

        var request = new CreateTopupRequestDto
        {
            BankAccountId = 99999, // Invalid ID
            Amount = 100000,
            TransferProof = mockFile.Object
        };

        // Act
        var result = await _controller.CreateTopupRequest(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;

        error.ErrorCode.Should().Be("BANK_ACCOUNT_NOT_FOUND");
    }

    [Fact]
    public async Task GetTopupHistory_WithAuthenticatedUser_ReturnsHistory()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var bankAccount = new BankAccount
        {
            BankName = "BCA",
            AccountNumber = "1234567890",
            AccountName = "Test Account"
        };
        _context.BankAccounts.Add(bankAccount);
        await _context.SaveChangesAsync();

        var topupRequest = new TopupRequest
        {
            Id = Guid.NewGuid(),
            UserId = user1.Id,
            BankAccountId = bankAccount.Id,
            Amount = 100000,
            TransferProofUrl = "/uploads/test.jpg",
            Status = TopupStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TopupRequests.Add(topupRequest);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetTopupHistory();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TopupHistoryResponse>().Subject;

        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.TotalRecords.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetBankAccounts_ReturnsAllBankAccounts()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        var bankAccount = new BankAccount
        {
            BankName = "BCA",
            AccountNumber = "1234567890",
            AccountName = "Test Account"
        };
        _context.BankAccounts.Add(bankAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetBankAccounts();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value;

        response.success.Should().Be(true);
        response.data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTopupHistory_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var user1 = await _context.Users.FirstAsync(u => u.Username == "user1");
        SetupAuthenticatedUser(user1.Id);

        // Act
        var result = await _controller.GetTopupHistory(page: 1, pageSize: 10);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TopupHistoryResponse>().Subject;

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
