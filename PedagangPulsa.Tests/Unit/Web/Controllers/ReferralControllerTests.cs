using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Web.Controllers;
using PedagangPulsa.Web.Areas.Admin.ViewModels;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class ReferralControllerTests
{
    private readonly Mock<ReferralService> _referralServiceMock;
    private readonly Mock<ILogger<ReferralController>> _loggerMock;
    private readonly ReferralController _controller;

    public ReferralControllerTests()
    {
        // Mock the DbContext to create ReferralService mock
        var dbContextMock = new Mock<PedagangPulsa.Infrastructure.Data.AppDbContext>(new Microsoft.EntityFrameworkCore.DbContextOptions<PedagangPulsa.Infrastructure.Data.AppDbContext>());
        var serviceLoggerMock = new Mock<ILogger<ReferralService>>();

        _referralServiceMock = new Mock<ReferralService>(dbContextMock.Object, serviceLoggerMock.Object);
        _loggerMock = new Mock<ILogger<ReferralController>>();

        _controller = new ReferralController(_referralServiceMock.Object, _loggerMock.Object);

        // Setup User for the controller
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Role, "Admin")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithTopReferrers()
    {
        // Arrange
        var expectedReferrers = new List<dynamic>
        {
            new { Username = "user1", TotalReferrals = 5, TotalBonus = 50000m }
        };

        _referralServiceMock
            .Setup(s => s.GetTopReferrersAsync(10))
            .ReturnsAsync(expectedReferrers);

        // Act
        var result = await _controller.Index() as ViewResult;

        // Assert
        result.Should().NotBeNull();
        result.ViewData["TopReferrers"].Should().BeEquivalentTo(expectedReferrers);
    }

    [Fact]
    public async Task GetData_ShouldReturnJsonWithFormattedData()
    {
        // Arrange
        var logs = new List<ReferralLog>
        {
            new ReferralLog
            {
                Id = Guid.NewGuid(),
                CreatedAt = new DateTime(2023, 1, 1, 10, 0, 0),
                Referrer = new User { Username = "referrer1" },
                Referee = new User { Username = "referee1" },
                BonusAmount = 10000,
                BonusStatus = ReferralBonusStatus.Pending
            }
        };

        _referralServiceMock
            .Setup(s => s.GetReferralLogsPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((logs, 1, 1));

        // Act
        var result = await _controller.GetData(1, 0, 10) as JsonResult;

        // Assert
        result.Should().NotBeNull();

        // Extract data from the anonymous object returned by Json()
        var value = result.Value;

        // Use reflection to get properties from the anonymous object
        var dataProperty = value.GetType().GetProperty("data");
        var data = dataProperty.GetValue(value) as IEnumerable<ReferralLogDataRow>;

        data.Should().NotBeNull();
        data.Should().HaveCount(1);

        var firstRow = data.First();
        firstRow.ReferrerUsername.Should().Be("referrer1");
        firstRow.RefereeUsername.Should().Be("referee1");
        firstRow.BonusAmount.Should().Be(10000);
        firstRow.BonusStatus.Should().Be("Pending");
        firstRow.CreatedAt.Should().Be("01 Jan 2023 10:00");
    }

    [Fact]
    public async Task PayBonus_WhenSuccessful_ShouldReturnSuccessJson()
    {
        // Arrange
        var logId = Guid.NewGuid();
        _referralServiceMock
            .Setup(s => s.PayPendingBonusAsync(logId, "testuser"))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.PayBonus(logId) as JsonResult;

        // Assert
        result.Should().NotBeNull();

        var value = result.Value;
        var successProperty = value.GetType().GetProperty("success");
        var success = (bool)successProperty.GetValue(value);

        success.Should().BeTrue();
    }

    [Fact]
    public async Task PayBonus_WhenFailed_ShouldReturnFailureJson()
    {
        // Arrange
        var logId = Guid.NewGuid();
        _referralServiceMock
            .Setup(s => s.PayPendingBonusAsync(logId, "testuser"))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.PayBonus(logId) as JsonResult;

        // Assert
        result.Should().NotBeNull();

        var value = result.Value;
        var successProperty = value.GetType().GetProperty("success");
        var success = (bool)successProperty.GetValue(value);

        success.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBonus_WhenSuccessful_ShouldReturnSuccessJson()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var reason = "Test cancellation";

        _referralServiceMock
            .Setup(s => s.CancelReferralBonusAsync(logId, reason, "testuser"))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelBonus(logId, reason) as JsonResult;

        // Assert
        result.Should().NotBeNull();

        var value = result.Value;
        var successProperty = value.GetType().GetProperty("success");
        var success = (bool)successProperty.GetValue(value);

        success.Should().BeTrue();
    }

    [Fact]
    public async Task CancelBonus_WhenFailed_ShouldReturnFailureJson()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var reason = "Test cancellation";

        _referralServiceMock
            .Setup(s => s.CancelReferralBonusAsync(logId, reason, "testuser"))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.CancelBonus(logId, reason) as JsonResult;

        // Assert
        result.Should().NotBeNull();

        var value = result.Value;
        var successProperty = value.GetType().GetProperty("success");
        var success = (bool)successProperty.GetValue(value);

        success.Should().BeFalse();
    }
}
