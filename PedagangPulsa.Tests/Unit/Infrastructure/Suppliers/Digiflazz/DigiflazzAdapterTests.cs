using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PedagangPulsa.Infrastructure.Suppliers.Digiflazz;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Infrastructure.Suppliers.Digiflazz
{
    public class DigiflazzAdapterTests
    {
        private readonly Mock<ILogger<DigiflazzAdapter>> _loggerMock;
        private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
        private readonly HttpClient _httpClient;
        private readonly DigiflazzAdapter _adapter;

        public DigiflazzAdapterTests()
        {
            _loggerMock = new Mock<ILogger<DigiflazzAdapter>>();
            _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.digiflazz.com")
            };

            _adapter = new DigiflazzAdapter(_loggerMock.Object, _httpClient);
        }

        [Fact]
        public async Task CheckBalanceAsync_WhenExceptionThrown_ShouldReturnFailedResult()
        {
            // Arrange
            var request = new SupplierBalanceRequest
            {
                SupplierId = Guid.NewGuid(),
                SupplierUsername = "testuser",
                SupplierApiKey = "testkey",
                SupplierApiUrl = "https://api.digiflazz.com/v1"
            };

            var exceptionMessage = "Network error";
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException(exceptionMessage));

            // Act
            var result = await _adapter.CheckBalanceAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be($"Error: {exceptionMessage}");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Balance check failed")),
                    It.IsAny<HttpRequestException>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task PurchaseAsync_WhenExceptionThrown_ShouldReturnFailedResult()
        {
            // Arrange
            var request = new SupplierPurchaseRequest
            {
                SupplierId = 1,
                SupplierUsername = "testuser",
                SupplierApiKey = "testkey",
                SupplierApiUrl = "https://api.digiflazz.com/v1",
                SupplierProductCode = "PULSA10",
                DestinationNumber = "08123456789",
                ReferenceId = Guid.NewGuid()
            };

            var exceptionMessage = "Timeout error";
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException(exceptionMessage));

            // Act
            var result = await _adapter.PurchaseAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be("SYSTEM_ERROR");
            result.Message.Should().Be(exceptionMessage);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during purchase with Digiflazz")),
                    It.IsAny<HttpRequestException>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task PingAsync_WhenExceptionThrown_ShouldReturnFailedResult()
        {
            // Arrange
            var exceptionMessage = "Connection refused";
            _httpMessageHandlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new HttpRequestException(exceptionMessage));

            // Act
            var result = await _adapter.PingAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Message.Should().Be(exceptionMessage);
            result.ResponseTimeMs.Should().Be(-1);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Ping failed for Digiflazz")),
                    It.IsAny<HttpRequestException>(),
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }
    }
}
