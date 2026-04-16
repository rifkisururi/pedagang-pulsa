using System;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Web.Controllers;
using PedagangPulsa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class ExportControllerTests
{
    private readonly Mock<ExportService> _exportServiceMock;
    private readonly Mock<ILogger<ExportController>> _loggerMock;
    private readonly ExportController _controller;

    public ExportControllerTests()
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(dbContextOptions);
        var exportLoggerMock = new Mock<ILogger<ExportService>>();

        _exportServiceMock = new Mock<ExportService>(dbContext, exportLoggerMock.Object) { CallBase = true };
        _loggerMock = new Mock<ILogger<ExportController>>();
        _controller = new ExportController(_exportServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportTransactions_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(s => s.ExportTransactionsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportTransactions();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonConvert.SerializeObject(badRequestResult.Value);
        var response = JObject.Parse(json);

        response["success"]!.Value<bool>().Should().BeFalse();
        response["message"]!.Value<string>().Should().Be("Error exporting transactions");
    }

    [Fact]
    public async Task ExportTopupRequests_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(s => s.ExportTopupRequestsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportTopupRequests();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonConvert.SerializeObject(badRequestResult.Value);
        var response = JObject.Parse(json);

        response["success"]!.Value<bool>().Should().BeFalse();
        response["message"]!.Value<string>().Should().Be("Error exporting topup requests");
    }

    [Fact]
    public async Task ExportBalanceLedger_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(s => s.ExportBalanceLedgerAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportBalanceLedger();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonConvert.SerializeObject(badRequestResult.Value);
        var response = JObject.Parse(json);

        response["success"]!.Value<bool>().Should().BeFalse();
        response["message"]!.Value<string>().Should().Be("Error exporting balance ledger");
    }

    [Fact]
    public async Task ExportProfitReport_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        _exportServiceMock.Setup(s => s.ExportProfitReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.ExportProfitReport(DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var json = JsonConvert.SerializeObject(badRequestResult.Value);
        var response = JObject.Parse(json);

        response["success"]!.Value<bool>().Should().BeFalse();
        response["message"]!.Value<string>().Should().Be("Error exporting profit report");
    }
}
