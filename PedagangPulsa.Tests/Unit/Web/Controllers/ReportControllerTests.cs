using System;
using System.Collections.Generic;
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

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class ReportControllerTests
{
    private readonly Mock<ReportService> _reportServiceMock;
    private readonly Mock<ILogger<ReportController>> _loggerMock;
    private readonly ReportController _controller;

    public ReportControllerTests()
    {
        // Mock dependencies for ReportService
        var dbContextMock = new Mock<AppDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext>());
        var loggerServiceMock = MockServices.CreateLogger<ReportService>();

        _reportServiceMock = new Mock<ReportService>(dbContextMock.Object, loggerServiceMock.Object);
        _loggerMock = MockServices.CreateLogger<ReportController>();

        _controller = new ReportController(_reportServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void Index_ReturnsViewResult()
    {
        // Act
        var result = _controller.Index();

        // Assert
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Daily_WithDefaultDate_UsesTodayAndReturnsPartialView()
    {
        // Arrange
        var defaultDate = default(DateTime);
        var expectedDate = DateTime.Today;
        var report = new DailyProfitReport { Date = expectedDate };

        _reportServiceMock.Setup(x => x.GetDailyProfitReportAsync(expectedDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.Daily(defaultDate);

        // Assert
        var partialViewResult = result.Should().BeOfType<PartialViewResult>().Subject;
        partialViewResult.ViewName.Should().Be("_DailyReport");
        partialViewResult.Model.Should().BeEquivalentTo(report);

        _reportServiceMock.Verify(x => x.GetDailyProfitReportAsync(expectedDate), Times.Once);
    }

    [Fact]
    public async Task Daily_WithSpecificDate_ReturnsPartialView()
    {
        // Arrange
        var specificDate = new DateTime(2024, 1, 1);
        var report = new DailyProfitReport { Date = specificDate };

        _reportServiceMock.Setup(x => x.GetDailyProfitReportAsync(specificDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.Daily(specificDate);

        // Assert
        var partialViewResult = result.Should().BeOfType<PartialViewResult>().Subject;
        partialViewResult.ViewName.Should().Be("_DailyReport");
        partialViewResult.Model.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task DailyData_WithDefaultDate_UsesTodayAndReturnsJson()
    {
        // Arrange
        var defaultDate = default(DateTime);
        var expectedDate = DateTime.Today;
        var report = new DailyProfitReport { Date = expectedDate };

        _reportServiceMock.Setup(x => x.GetDailyProfitReportAsync(expectedDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.DailyData(defaultDate);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value!;

        var successProp = response.GetType().GetProperty("success")!;
        var dataProp = response.GetType().GetProperty("data")!;

        var success = (bool)successProp.GetValue(response, null)!;
        var data = dataProp.GetValue(response, null)!;


        Assert.True(success);
        Assert.Equal(report, data);
    }

    [Fact]
    public async Task DailyData_WithSpecificDate_ReturnsJson()
    {
        // Arrange
        var specificDate = new DateTime(2024, 1, 1);
        var report = new DailyProfitReport { Date = specificDate };

        _reportServiceMock.Setup(x => x.GetDailyProfitReportAsync(specificDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.DailyData(specificDate);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value!;

        var successProp = response.GetType().GetProperty("success")!;
        var dataProp = response.GetType().GetProperty("data")!;

        var success = (bool)successProp.GetValue(response, null)!;
        var data = dataProp.GetValue(response, null)!;


        Assert.True(success);
        Assert.Equal(report, data);
    }

    [Fact]
    public async Task SummaryData_ReturnsJsonWithData()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var summary = new List<DailyProfitSummary>
        {
            new DailyProfitSummary { Date = startDate }
        };

        _reportServiceMock.Setup(x => x.GetDailyProfitSummaryAsync(startDate, endDate))
            .ReturnsAsync(summary);

        // Act
        var result = await _controller.SummaryData(startDate, endDate);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value!;

        var successProp = response.GetType().GetProperty("success")!;
        var dataProp = response.GetType().GetProperty("data")!;

        var success = (bool)successProp.GetValue(response, null)!;
        var data = dataProp.GetValue(response, null)!;


        Assert.True(success);
        Assert.Equal(summary, data);
    }

    [Fact]
    public async Task BySupplier_ReturnsPartialView()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var report = new ProfitBySupplierReport { StartDate = startDate, EndDate = endDate };

        _reportServiceMock.Setup(x => x.GetProfitBySupplierAsync(startDate, endDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.BySupplier(startDate, endDate);

        // Assert
        var partialViewResult = result.Should().BeOfType<PartialViewResult>().Subject;
        partialViewResult.ViewName.Should().Be("_BySupplierReport");
        partialViewResult.Model.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task BySupplierData_ReturnsJsonWithData()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var report = new ProfitBySupplierReport { StartDate = startDate, EndDate = endDate };

        _reportServiceMock.Setup(x => x.GetProfitBySupplierAsync(startDate, endDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.BySupplierData(startDate, endDate);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value!;

        var successProp = response.GetType().GetProperty("success")!;
        var dataProp = response.GetType().GetProperty("data")!;

        var success = (bool)successProp.GetValue(response, null)!;
        var data = dataProp.GetValue(response, null)!;


        Assert.True(success);
        Assert.Equal(report, data);
    }

    [Fact]
    public async Task ByProduct_ReturnsPartialView()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var report = new ProfitByProductReport { StartDate = startDate, EndDate = endDate };

        _reportServiceMock.Setup(x => x.GetProfitByProductAsync(startDate, endDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.ByProduct(startDate, endDate);

        // Assert
        var partialViewResult = result.Should().BeOfType<PartialViewResult>().Subject;
        partialViewResult.ViewName.Should().Be("_ByProductReport");
        partialViewResult.Model.Should().BeEquivalentTo(report);
    }

    [Fact]
    public async Task ByProductData_ReturnsJsonWithData()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 1, 31);
        var report = new ProfitByProductReport { StartDate = startDate, EndDate = endDate };

        _reportServiceMock.Setup(x => x.GetProfitByProductAsync(startDate, endDate))
            .ReturnsAsync(report);

        // Act
        var result = await _controller.ByProductData(startDate, endDate);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        var response = jsonResult.Value!;

        var successProp = response.GetType().GetProperty("success")!;
        var dataProp = response.GetType().GetProperty("data")!;

        var success = (bool)successProp.GetValue(response, null)!;
        var data = dataProp.GetValue(response, null)!;


        Assert.True(success);
        Assert.Equal(report, data);
    }
}
