using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
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
    private readonly Mock<ILogger<ExportController>> _loggerMock;
    private readonly Mock<ExportService> _exportServiceMock;
    private readonly ExportController _controller;

    public ExportControllerTests()
    {
        _loggerMock = new Mock<ILogger<ExportController>>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        var loggerServiceMock = new Mock<ILogger<ExportService>>();

        _exportServiceMock = new Mock<ExportService>(context, loggerServiceMock.Object);

        _controller = new ExportController(_exportServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportProfitReport_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var exception = new Exception("Test exception");

        _exportServiceMock
            .Setup(s => s.ExportProfitReportAsync(startDate, endDate))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportProfitReport(startDate, endDate);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;

        var successProp = value?.GetType().GetProperty("success");
        var success = (bool)(successProp?.GetValue(value) ?? false);
        success.Should().BeFalse();

        var messageProp = value?.GetType().GetProperty("message");
        var message = (string?)(messageProp?.GetValue(value) ?? "");
        message.Should().Be("Error exporting profit report");

        // Verify logger was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error exporting profit report")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportTransactions_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var exception = new Exception("Test exception");

        _exportServiceMock
            .Setup(s => s.ExportTransactionsAsync(startDate, endDate, null, null))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportTransactions(startDate, endDate, null, null);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;

        var successProp = value?.GetType().GetProperty("success");
        var success = (bool)(successProp?.GetValue(value) ?? false);
        success.Should().BeFalse();

        var messageProp = value?.GetType().GetProperty("message");
        var message = (string?)(messageProp?.GetValue(value) ?? "");
        message.Should().Be("Error exporting transactions");

        // Verify logger was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error exporting transactions")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportTopupRequests_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var exception = new Exception("Test exception");

        _exportServiceMock
            .Setup(s => s.ExportTopupRequestsAsync(startDate, endDate, null))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportTopupRequests(startDate, endDate, null);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;

        var successProp = value?.GetType().GetProperty("success");
        var success = (bool)(successProp?.GetValue(value) ?? false);
        success.Should().BeFalse();

        var messageProp = value?.GetType().GetProperty("message");
        var message = (string?)(messageProp?.GetValue(value) ?? "");
        message.Should().Be("Error exporting topup requests");

        // Verify logger was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error exporting topup requests")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExportBalanceLedger_WhenExceptionThrown_ReturnsBadRequest()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var exception = new Exception("Test exception");

        _exportServiceMock
            .Setup(s => s.ExportBalanceLedgerAsync(startDate, endDate, null, null))
            .ThrowsAsync(exception);

        // Act
        var result = await _controller.ExportBalanceLedger(startDate, endDate, null, null);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var value = badRequestResult.Value;

        var successProp = value?.GetType().GetProperty("success");
        var success = (bool)(successProp?.GetValue(value) ?? false);
        success.Should().BeFalse();

        var messageProp = value?.GetType().GetProperty("message");
        var message = (string?)(messageProp?.GetValue(value) ?? "");
        message.Should().Be("Error exporting balance ledger");

        // Verify logger was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error exporting balance ledger")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
