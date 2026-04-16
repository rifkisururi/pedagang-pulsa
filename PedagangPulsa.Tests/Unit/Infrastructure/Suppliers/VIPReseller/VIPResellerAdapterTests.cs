using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PedagangPulsa.Infrastructure.Suppliers.DTOs;
using PedagangPulsa.Infrastructure.Suppliers.VIPReseller;
using Xunit;
using System.Text.Json;

namespace PedagangPulsa.Tests.Unit.Infrastructure.Suppliers.VIPReseller;

public class VIPResellerAdapterTests
{
    private readonly Mock<ILogger<VIPResellerAdapter>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public VIPResellerAdapterTests()
    {
        _loggerMock = new Mock<ILogger<VIPResellerAdapter>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
    }

    private VIPResellerAdapter CreateAdapter()
    {
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://vip-reseller.co.id/api/")
        };
        return new VIPResellerAdapter(_loggerMock.Object, httpClient);
    }

    [Fact]
    public async Task PurchaseAsync_ShouldReturnSystemError_WhenExceptionIsThrown()
    {
        // Arrange
        var request = new SupplierPurchaseRequest
        {
            DestinationNumber = "08123456789",
            SupplierProductCode = "PULSA10",
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            ReferenceId = Guid.NewGuid(),
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection error"));

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.PurchaseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SYSTEM_ERROR");
        result.Message.Should().Be("Connection error");
    }

    [Fact]
    public async Task PurchaseAsync_ShouldReturnParseError_WhenResponseIsInvalid()
    {
        // Arrange
        var request = new SupplierPurchaseRequest
        {
            DestinationNumber = "08123456789",
            SupplierProductCode = "PULSA10",
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            ReferenceId = Guid.NewGuid(),
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null") // This will deserialize to null
            });

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.PurchaseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("PARSE_ERROR");
        result.Message.Should().Be("Failed to parse supplier response");
    }

    [Fact]
    public async Task PurchaseAsync_ShouldReturnSuccess_WhenResponseIsSuccessful()
    {
        // Arrange
        var request = new SupplierPurchaseRequest
        {
            DestinationNumber = "08123456789",
            SupplierProductCode = "PULSA10",
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            ReferenceId = Guid.NewGuid(),
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        var responsePayload = new
        {
            Status = true,
            Message = "Success",
            Code = "OK",
            TrxId = "trx123",
            Sn = "sn123"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responsePayload))
            });

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.PurchaseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ErrorCode.Should().BeEmpty();
        result.Message.Should().Be("Success");
        result.SupplierTransactionId.Should().Be("trx123");
        result.SerialNumber.Should().Be("sn123");
        result.SupplierMessage.Should().Be("Success");
    }

    [Fact]
    public async Task PurchaseAsync_ShouldReturnError_WhenResponseStatusIsFalse()
    {
        // Arrange
        var request = new SupplierPurchaseRequest
        {
            DestinationNumber = "08123456789",
            SupplierProductCode = "PULSA10",
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            ReferenceId = Guid.NewGuid(),
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        var responsePayload = new
        {
            Status = false,
            Message = "Failed",
            Code = "ERR01"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responsePayload))
            });

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.PurchaseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR01");
        result.Message.Should().Be("Failed");
    }

    [Fact]
    public async Task CheckBalanceAsync_ShouldReturnError_WhenExceptionIsThrown()
    {
        // Arrange
        var request = new SupplierBalanceRequest
        {
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection error"));

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.CheckBalanceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Error: Connection error");
    }

    [Fact]
    public async Task CheckBalanceAsync_ShouldReturnError_WhenResponseIsInvalid()
    {
        // Arrange
        var request = new SupplierBalanceRequest
        {
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"Data\":null}")
            });

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.CheckBalanceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Failed to parse balance response");
    }

    [Fact]
    public async Task CheckBalanceAsync_ShouldReturnSuccess_WhenResponseIsSuccessful()
    {
        // Arrange
        var request = new SupplierBalanceRequest
        {
            SupplierApiKey = "dummy-api-key",
            SupplierUsername = "dummy-username",
            SupplierApiUrl = "https://vip-reseller.co.id/api"
        };

        var responsePayload = new
        {
            Data = new
            {
                Balance = 150000m
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responsePayload))
            });

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.CheckBalanceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Balance.Should().Be(150000m);
        result.Message.Should().Be("Balance: Rp 150,000");
    }

    [Fact]
    public async Task PingAsync_ShouldReturnSuccess_WhenApiIsReachable()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.PingAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Connected");
        result.ResponseTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task PingAsync_ShouldReturnError_WhenExceptionIsThrown()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection timeout"));

        var adapter = CreateAdapter();

        // Act
        var result = await adapter.PingAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Connection timeout");
        result.ResponseTimeMs.Should().Be(-1);
    }
}
