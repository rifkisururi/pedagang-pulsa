using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Web.Controllers;
using PedagangPulsa.Tests.Helpers;
using Xunit;
using PedagangPulsa.Infrastructure.Data;

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class ExportControllerTests
{
    private readonly Mock<ExportService> _exportServiceMock;
    private readonly Mock<ILogger<ExportController>> _loggerMock;
    private readonly ExportController _controller;

    public ExportControllerTests()
    {
        var dbContextOptions = new Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext>();
        var testDbContext = new TestDbContext(); // ExportService needs AppDbContext but we can pass null or dummy if we mock the methods
        var exportLoggerMock = new Mock<ILogger<ExportService>>();

        // Setup mock ExportService to not hit the database
        _exportServiceMock = new Mock<ExportService>(testDbContext, exportLoggerMock.Object);
        _loggerMock = MockServices.CreateLogger<ExportController>();

        _controller = new ExportController(_exportServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportTransactions_ShouldReturnBadRequest_WhenExceptionThrown()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _exportServiceMock.Setup(x => x.ExportTransactionsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportTransactions();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting transactions" });
        MockServices.VerifyLogCall(_loggerMock, LogLevel.Error, "Error exporting transactions", Times.Once());
    }

    [Fact]
    public async Task ExportTopupRequests_ShouldReturnBadRequest_WhenExceptionThrown()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _exportServiceMock.Setup(x => x.ExportTopupRequestsAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportTopupRequests();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting topup requests" });
        MockServices.VerifyLogCall(_loggerMock, LogLevel.Error, "Error exporting topup requests", Times.Once());
    }

    [Fact]
    public async Task ExportBalanceLedger_ShouldReturnBadRequest_WhenExceptionThrown()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _exportServiceMock.Setup(x => x.ExportBalanceLedgerAsync(
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportBalanceLedger();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting balance ledger" });
        MockServices.VerifyLogCall(_loggerMock, LogLevel.Error, "Error exporting balance ledger", Times.Once());
    }

    [Fact]
    public async Task ExportProfitReport_ShouldReturnBadRequest_WhenExceptionThrown()
    {
        // Arrange
        var exception = new Exception("Test exception");
        _exportServiceMock.Setup(x => x.ExportProfitReportAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportProfitReport(DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting profit report" });
        MockServices.VerifyLogCall(_loggerMock, LogLevel.Error, "Error exporting profit report", Times.Once());
    }
}
