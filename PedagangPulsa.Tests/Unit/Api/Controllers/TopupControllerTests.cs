using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Api.Controllers;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Tests.Helpers;
using System.Security.Claims;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Api.Controllers;

public class TopupControllerTests : IAsyncDisposable
{
    private readonly TestDbContext _context;
    private readonly TopupService _topupService;
    private readonly Mock<ILogger<TopupController>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly TopupController _controller;
    private readonly string _webRootPath;

    public TopupControllerTests()
    {
        _context = new TestDbContext();
        _context.SeedAsync().Wait();

        _topupService = new TopupService(_context);
        _loggerMock = MockServices.CreateLogger<TopupController>();
        _configurationMock = new Mock<IConfiguration>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _webRootPath = Path.Combine(Path.GetTempPath(), $"pedagangpulsa-topup-tests-{Guid.NewGuid():N}");

        _environmentMock.Setup(e => e.WebRootPath).Returns(_webRootPath);
        _environmentMock.Setup(e => e.ContentRootPath).Returns(_webRootPath);

        _controller = new TopupController(
            _context,
            _topupService,
            _loggerMock.Object,
            _configurationMock.Object,
            _environmentMock.Object);
    }

    [Fact]
    public async Task RequestTopup_WithValidData_ReturnsCreated()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        var request = new CreateTopupRequestDto
        {
            BankAccountId = 1,
            Amount = 100000
        };

        var result = await _controller.RequestTopup(request);

        var createdResult = result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(201);

        var response = createdResult.Value.Should().BeOfType<TopupRequestResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Status.Should().Be("pending");
        response.Payment.BankName.Should().Be("BCA");
        response.Payment.OriginalAmount.Should().Be(100000);
        response.Payment.TotalAmount.Should().Be(response.Payment.OriginalAmount + response.Payment.UniqueCode);

        var persistedTopup = await _context.TopupRequests.FirstOrDefaultAsync(t => t.Id == response.TopupId);
        persistedTopup.Should().NotBeNull();
        persistedTopup!.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task RequestTopup_WithInvalidBankAccount_ReturnsNotFound()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        var request = new CreateTopupRequestDto
        {
            BankAccountId = 99999,
            Amount = 100000
        };

        var result = await _controller.RequestTopup(request);

        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var error = notFoundResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.ErrorCode.Should().Be("BANK_ACCOUNT_NOT_FOUND");
    }

    [Fact]
    public async Task UploadTransferProof_WithInvalidFileType_ReturnsBadRequest()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        var topupRequest = await _topupService.CreateTopupRequestAsync(user.Id, 1, 100000);
        topupRequest.Should().NotBeNull();

        var mockFile = CreateMockFile("proof.exe", "application/octet-stream", [0x01, 0x02, 0x03]);

        var result = await _controller.UploadTransferProof(topupRequest!.Id, mockFile.Object);

        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.ErrorCode.Should().Be("INVALID_FILE_TYPE");
    }

    [Fact]
    public async Task UploadTransferProof_WithValidFile_UpdatesTopupRequest()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        var topupRequest = await _topupService.CreateTopupRequestAsync(user.Id, 1, 100000);
        topupRequest.Should().NotBeNull();

        var mockFile = CreateMockFile("proof.jpg", "image/jpeg", [0x01, 0x02, 0x03, 0x04]);

        var result = await _controller.UploadTransferProof(topupRequest!.Id, mockFile.Object);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TransferProofResponse>().Subject;
        response.Success.Should().BeTrue();
        response.TopupId.Should().Be(topupRequest.Id);

        var persistedTopup = await _context.TopupRequests.FirstAsync(t => t.Id == topupRequest.Id);
        persistedTopup.TransferProofUrl.Should().StartWith("/uploads/topup/");
    }

    [Fact]
    public async Task GetTopupHistory_WithAuthenticatedUser_ReturnsHistory()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        await _topupService.CreateTopupRequestAsync(user.Id, 1, 100000);

        var result = await _controller.GetTopupHistory();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TopupHistoryResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeEmpty();
        response.TotalRecords.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTopupHistory_WithPagination_ReturnsCorrectPage()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        await _topupService.CreateTopupRequestAsync(user.Id, 1, 100000);

        var result = await _controller.GetTopupHistory(page: 1, pageSize: 10);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<TopupHistoryResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetTopupDetail_WithExistingTopup_ReturnsDetail()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        var topupRequest = await _topupService.CreateTopupRequestAsync(user.Id, 1, 100000);
        topupRequest.Should().NotBeNull();

        var result = await _controller.GetTopupDetail(topupRequest!.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value!;

        ((bool)response.success).Should().BeTrue();
        ((Guid)response.data.id).Should().Be(topupRequest.Id);
        ((decimal)response.data.amount).Should().Be(100000);
        ((string)response.data.status).Should().Be(TopupStatus.Pending.ToString().ToLowerInvariant());
    }

    [Fact]
    public async Task GetBankAccounts_ReturnsAllActiveBankAccounts()
    {
        var user = await _context.Users.FirstAsync(u => u.UserName == "user1");
        SetupAuthenticatedUser(user.Id);

        var result = await _controller.GetBankAccounts();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic response = okResult.Value!;

        ((bool)response.success).Should().BeTrue();
        response.data.Should().NotBeNull();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.CleanAsync();
        await _context.DisposeAsync();

        if (Directory.Exists(_webRootPath))
        {
            Directory.Delete(_webRootPath, recursive: true);
        }
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

    private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((stream, _) => stream.Write(content, 0, content.Length))
            .Returns(Task.CompletedTask);

        return mockFile;
    }
}
