using System;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Web.Controllers;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Web.Controllers;

public class DashboardControllerTests
{
    private readonly Mock<ILogger<DashboardController>> _loggerMock;
    private readonly DashboardController _sut;

    public DashboardControllerTests()
    {
        _loggerMock = new Mock<ILogger<DashboardController>>();
        _sut = new DashboardController(_loggerMock.Object);
    }

    [Fact]
    public void Index_ShouldReturnViewResult()
    {
        // Act
        var result = _sut.Index();

        // Assert
        result.Should().BeOfType<ViewResult>();
    }
}
