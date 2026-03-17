using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Infrastructure.Suppliers;

namespace PedagangPulsa.Tests.Helpers;

/// <summary>
/// Common mock service factories
/// </summary>
public static class MockServices
{
    /// <summary>
    /// Create a mock logger
    /// </summary>
    public static Mock<ILogger<T>> CreateLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }

    /// <summary>
    /// Create a mock logger factory
    /// </summary>
    public static Mock<ILoggerFactory> CreateLoggerFactory()
    {
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
        return loggerFactoryMock;
    }

    /// <summary>
    /// Create a mock supplier adapter factory
    /// </summary>
    public static Mock<ISupplierAdapterFactory> CreateSupplierAdapterFactory()
    {
        return new Mock<ISupplierAdapterFactory>();
    }

    /// <summary>
    /// Setup logger to log messages (for debugging tests)
    /// </summary>
    public static void SetupLoggerLogs<T>(Mock<ILogger<T>> loggerMock)
    {
        loggerMock.Setup(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            )
        ).Callback(new InvocationAction(invocation =>
        {
            var logLevel = (LogLevel)invocation.Arguments[0]!;
            var state = invocation.Arguments[2]!;
            var exception = (Exception?)invocation.Arguments[3];

            // For debugging tests, you can write to console
            // Console.WriteLine($"[{logLevel}] {state}");
        }));
    }

    /// <summary>
    /// Verify that a log was called with specific log level
    /// </summary>
    public static void VerifyLogCall<T>(Mock<ILogger<T>> loggerMock, LogLevel logLevel, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            times);
    }

    /// <summary>
    /// Verify that a log was called with specific log level and message
    /// </summary>
    public static void VerifyLogCall<T>(Mock<ILogger<T>> loggerMock, LogLevel logLevel, string message, Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v!.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            times);
    }
}
