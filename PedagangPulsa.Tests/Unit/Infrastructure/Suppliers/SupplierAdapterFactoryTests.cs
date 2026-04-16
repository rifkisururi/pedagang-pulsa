using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Infrastructure.Suppliers;
using PedagangPulsa.Infrastructure.Suppliers.Digiflazz;
using PedagangPulsa.Infrastructure.Suppliers.VIPReseller;
using Xunit;

namespace PedagangPulsa.Tests.Unit.Infrastructure.Suppliers;

public class SupplierAdapterFactoryTests
{
    private readonly Mock<ILogger<SupplierAdapterFactory>> _loggerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly SupplierAdapterFactory _sut;

    public SupplierAdapterFactoryTests()
    {
        _loggerMock = new Mock<ILogger<SupplierAdapterFactory>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();

        // Setup mock logger factory to return mock loggers to avoid exceptions during normal creation
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        _sut = new SupplierAdapterFactory(_loggerMock.Object);
    }

    [Fact]
    public void CreateAdapter_WithDigiflazzCode_ReturnsDigiflazzAdapter()
    {
        // Act
        var result = _sut.CreateAdapter("DIGIFLAZZ", _loggerFactoryMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DigiflazzAdapter>();
    }

    [Fact]
    public void CreateAdapter_WithVipResellerCode_ReturnsVipResellerAdapter()
    {
        // Act
        var result = _sut.CreateAdapter("VIPRESELLER", _loggerFactoryMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<VIPResellerAdapter>();
    }

    [Fact]
    public void CreateAdapter_WithLowercaseCode_ReturnsCorrectAdapter()
    {
        // Act
        var result = _sut.CreateAdapter("digiflazz", _loggerFactoryMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<DigiflazzAdapter>();
    }

    [Fact]
    public void CreateAdapter_WithUnknownCode_ReturnsNull()
    {
        // Act
        var result = _sut.CreateAdapter("UNKNOWN", _loggerFactoryMock.Object);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CreateAdapter_WithNullCode_CatchesExceptionAndReturnsNull()
    {
        // Act
        var result = _sut.CreateAdapter(null!, _loggerFactoryMock.Object);

        // Assert
        result.Should().BeNull();
        VerifyLoggerErrorCalled();
    }

    [Fact]
    public void CreateAdapter_WhenLoggerFactoryThrows_CatchesExceptionAndReturnsNull()
    {
        // Arrange
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Throws(new InvalidOperationException("Test exception"));

        // Act
        var result = _sut.CreateAdapter("DIGIFLAZZ", _loggerFactoryMock.Object);

        // Assert
        result.Should().BeNull();
        VerifyLoggerErrorCalled();
    }

    private void VerifyLoggerErrorCalled()
    {
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
