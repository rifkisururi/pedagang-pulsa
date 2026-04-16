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

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class ExportControllerTests
{
    private readonly Mock<ExportService> _exportServiceMock;
    private readonly Mock<ILogger<ExportController>> _loggerMock;
    private readonly ExportController _controller;

    public ExportControllerTests()
    {
        var dbContext = new TestDbContext();
        var serviceLoggerMock = new Mock<ILogger<ExportService>>();
        _exportServiceMock = new Mock<ExportService>(dbContext, serviceLoggerMock.Object);
        _loggerMock = new Mock<ILogger<ExportController>>();
        _controller = new ExportController(_exportServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportTransactions_ThrowsException_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(x => x.ExportTransactionsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportTransactions();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting transactions" });
    }

    [Fact]
    public async Task ExportTopupRequests_ThrowsException_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(x => x.ExportTopupRequestsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportTopupRequests();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting topup requests" });
    }

    [Fact]
    public async Task ExportBalanceLedger_ThrowsException_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(x => x.ExportBalanceLedgerAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportBalanceLedger();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting balance ledger" });
    }

    [Fact]
    public async Task ExportProfitReport_ThrowsException_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(x => x.ExportProfitReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportProfitReport(DateTime.Now, DateTime.Now);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting profit report" });
    }
}
