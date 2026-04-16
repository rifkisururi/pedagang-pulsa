using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PedagangPulsa.Infrastructure.Suppliers;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Infrastructure.Suppliers;

public class SupplierAdapterBaseTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly TestSupplierAdapter _adapter;

    public SupplierAdapterBaseTests()
    {
        _loggerMock = new Mock<ILogger>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.testsupplier.com")
        };

        _adapter = new TestSupplierAdapter(_loggerMock.Object, httpClient);
    }

    [Fact]
    public async Task HandleExceptionAsync_ReturnsFailedPurchaseResult()
    {
        // Arrange
        var testOperation = "TestOperation";
        var exceptionMessage = "Test exception message";
        var exception = new InvalidOperationException(exceptionMessage);

        // Act
        var result = await _adapter.TestHandleExceptionAsync(exception, testOperation);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SYSTEM_ERROR");
        result.Message.Should().Be(exceptionMessage);

        // Verify logger was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Error during {testOperation} with TestSupplier")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PingAsync_WithException_ReturnsFailedResult()
    {
        // Arrange
        var testUrl = "https://api.testsupplier.com/ping";
        var exceptionMessage = "Network error occurred";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException(exceptionMessage));

        // Act
        var result = await _adapter.TestPingAsync(testUrl);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be(exceptionMessage);
        result.ResponseTimeMs.Should().Be(-1);

        // Verify logger was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Ping failed for TestSupplier")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // A concrete implementation of the abstract class for testing
    private class TestSupplierAdapter : SupplierAdapterBase
    {
        public TestSupplierAdapter(ILogger logger, HttpClient httpClient)
            : base(logger, httpClient)
        {
        }

        public override string SupplierName => "TestSupplier";
        public override string SupplierCode => "TS";

        public override Task<SupplierPurchaseResult> PurchaseAsync(SupplierPurchaseRequest request)
            => throw new NotImplementedException();

        public override Task<SupplierBalanceResult> CheckBalanceAsync(SupplierBalanceRequest request)
            => throw new NotImplementedException();

        public override Task<SupplierPingResult> PingAsync()
            => throw new NotImplementedException();

        // Expose the protected methods for testing
        public Task<SupplierPurchaseResult> TestHandleExceptionAsync(Exception ex, string operation)
        {
            return HandleExceptionAsync(ex, operation);
        }

        public Task<SupplierPingResult> TestPingAsync(string url)
        {
            return PingAsync(url);
        }
    }
}
