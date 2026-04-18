using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PedagangPulsa.Infrastructure.Data.Interceptors;

public class EfCoreErrorLogger : DbCommandInterceptor
{
    private readonly ILogger<EfCoreErrorLogger> _logger;

    public EfCoreErrorLogger(ILogger<EfCoreErrorLogger> logger)
    {
        _logger = logger;
    }

    public override void CommandFailed(
        DbCommand command,
        CommandErrorEventData eventData)
    {
        LogCommandError(command, eventData.Exception);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogCommandError(command, eventData.Exception);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void LogCommandError(DbCommand command, Exception exception)
    {
        _logger.LogError(exception, "Database query failed. Query: {CommandText}", command.CommandText);
    }
}
