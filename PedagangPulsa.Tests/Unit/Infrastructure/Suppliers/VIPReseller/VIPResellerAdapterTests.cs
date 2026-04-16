using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;
using PedagangPulsa.Infrastructure.Suppliers.VIPReseller;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Infrastructure.Suppliers.VIPReseller;

public class VIPResellerAdapterTests
{
    private readonly Mock<ILogger<VIPResellerAdapter>> _loggerMock;

    public VIPResellerAdapterTests()
    {
        _loggerMock = new Mock<ILogger<VIPResellerAdapter>>();
    }

    [Fact]
    public async Task CheckBalanceAsync_WhenExceptionThrown_ReturnsFailureAndLogsError()
    {
        // Arrange
        var request = new SupplierBalanceRequest
        {
            SupplierApiUrl = "https://api.vipreseller.co.id",
            SupplierApiKey = "api_key",
            SupplierUsername = "username"
        };

        var expectedException = new HttpRequestException("Network error");
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(expectedException);

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new VIPResellerAdapter(_loggerMock.Object, httpClient);

        // Act
        var result = await sut.CheckBalanceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Error:");
        result.Message.Should().Contain("Network error");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Balance check failed")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PurchaseAsync_WhenExceptionThrown_ReturnsFailureAndLogsError()
    {
        // Arrange
        var request = new SupplierPurchaseRequest
        {
            SupplierApiUrl = "https://api.vipreseller.co.id",
            SupplierApiKey = "api_key",
            SupplierUsername = "username",
            SupplierProductCode = "PULSA10",
            DestinationNumber = "08123456789",
            ReferenceId = Guid.NewGuid(),
            TimeoutSeconds = 30
        };

        var expectedException = new HttpRequestException("Network error during purchase");
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(expectedException);

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new VIPResellerAdapter(_loggerMock.Object, httpClient);

        // Act
        var result = await sut.PurchaseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SYSTEM_ERROR");
        result.Message.Should().Contain("Network error during purchase");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during purchase")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PingAsync_WhenExceptionThrown_ReturnsFailureAndLogsError()
    {
        // Arrange
        var expectedException = new HttpRequestException("Ping network error");
        var handlerMock = new Mock<HttpMessageHandler>();

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(expectedException);

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new VIPResellerAdapter(_loggerMock.Object, httpClient);

        // Act
        var result = await sut.PingAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Ping network error");
        result.ResponseTimeMs.Should().Be(-1);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Ping failed")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
