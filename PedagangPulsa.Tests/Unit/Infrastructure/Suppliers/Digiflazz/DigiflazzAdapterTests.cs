using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PedagangPulsa.Infrastructure.Suppliers.Digiflazz;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;
using Xunit;
using FluentAssertions;

namespace PedagangPulsa.Tests.Unit.Infrastructure.Suppliers.Digiflazz;

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
    public async Task PurchaseAsync_ThrowsException_ReturnsSystemError()
    {
        // Arrange
        var request = new SupplierPurchaseRequest
        {
            DestinationNumber = "081234567890",
            SupplierProductCode = "PULSA10",
            SupplierUsername = "testuser",
            SupplierApiKey = "testapikey",
            SupplierApiUrl = "https://api.digiflazz.com",
            ReferenceId = Guid.NewGuid()
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _adapter.PurchaseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SYSTEM_ERROR");
        result.Message.Should().Be("Network error");
    }

    [Fact]
    public async Task CheckBalanceAsync_ThrowsException_ReturnsFailedResult()
    {
        // Arrange
        var request = new SupplierBalanceRequest
        {
            SupplierId = Guid.NewGuid(),
            SupplierUsername = "testuser",
            SupplierApiKey = "testapikey",
            SupplierApiUrl = "https://api.digiflazz.com"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _adapter.CheckBalanceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Error: Network error");
    }

    [Fact]
    public async Task PingAsync_ThrowsException_ReturnsFailedResult()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _adapter.PingAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Network error");
        result.ResponseTimeMs.Should().Be(-1);
    }
}
