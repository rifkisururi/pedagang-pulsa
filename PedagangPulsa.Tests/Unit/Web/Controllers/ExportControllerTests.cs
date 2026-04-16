using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Web.Controllers;
using PedagangPulsa.Tests.Helpers;
using Xunit;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using System.Collections.Generic;

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class ExportControllerTests
{
    private readonly ExportController _controller;
    private readonly Mock<ILogger<ExportController>> _loggerMock;
    private readonly ExportService _exportService;
    private readonly AppDbContext _dbContext;

    public ExportControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options);

        var exportLoggerMock = new Mock<ILogger<ExportService>>();
        _exportService = new ExportService(_dbContext, exportLoggerMock.Object);

        _loggerMock = new Mock<ILogger<ExportController>>();
        _controller = new ExportController(_exportService, _loggerMock.Object);
    }

    [Fact]
    public async Task ExportTransactions_ShouldReturnFileResult()
    {
        // Act
        var result = await _controller.ExportTransactions(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().StartWith("Transactions_");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportTransactions_WhenExceptionOccurs_ShouldReturnBadRequest()
    {
        // Arrange
        var controllerWithNullService = new ExportController(null, _loggerMock.Object);

        // Act
        var result = await controllerWithNullService.ExportTransactions();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting transactions" });
    }

    [Fact]
    public async Task ExportTopupRequests_ShouldReturnFileResult()
    {
        // Act
        var result = await _controller.ExportTopupRequests(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().StartWith("TopupRequests_");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportTopupRequests_WhenExceptionOccurs_ShouldReturnBadRequest()
    {
        // Arrange
        var controllerWithNullService = new ExportController(null, _loggerMock.Object);

        // Act
        var result = await controllerWithNullService.ExportTopupRequests();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting topup requests" });
    }

    [Fact]
    public async Task ExportBalanceLedger_ShouldReturnFileResult()
    {
        // Act
        var result = await _controller.ExportBalanceLedger(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().StartWith("BalanceLedger_");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportBalanceLedger_WhenExceptionOccurs_ShouldReturnBadRequest()
    {
        // Arrange
        var controllerWithNullService = new ExportController(null, _loggerMock.Object);

        // Act
        var result = await controllerWithNullService.ExportBalanceLedger();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting balance ledger" });
    }

    [Fact]
    public async Task ExportProfitReport_ShouldReturnFileResult()
    {
        // Act
        var result = await _controller.ExportProfitReport(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().StartWith("ProfitReport_");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExportProfitReport_WhenExceptionOccurs_ShouldReturnBadRequest()
    {
        // Arrange
        var controllerWithNullService = new ExportController(null, _loggerMock.Object);

        // Act
        var result = await controllerWithNullService.ExportProfitReport(DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequestResult.Value.Should().BeEquivalentTo(new { success = false, message = "Error exporting profit report" });
    }
}
