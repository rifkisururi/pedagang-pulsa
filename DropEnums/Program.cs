using Microsoft.Extensions.Logging;
using Npgsql;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

var logger = loggerFactory.CreateLogger<Program>();

var connectionString = "Host=ep-cold-field-a111o43u-pooler.ap-southeast-1.aws.neon.tech;Username=neondb_owner;Password=npg_hWbvwU2O5Bur;Database=neondb;SSL Mode=Require;Trust Server Certificate=true";

var dropCommands = new[]
{
    "DROP TYPE IF EXISTS admin_role CASCADE;",
    "DROP TYPE IF EXISTS attempt_status CASCADE;",
    "DROP TYPE IF EXISTS balance_tx_type CASCADE;",
    "DROP TYPE IF EXISTS markup_type CASCADE;",
    "DROP TYPE IF EXISTS notification_channel CASCADE;",
    "DROP TYPE IF EXISTS referral_bonus_status CASCADE;",
    "DROP TYPE IF EXISTS topup_status CASCADE;",
    "DROP TYPE IF EXISTS transaction_status CASCADE;",
    "DROP TYPE IF EXISTS user_status CASCADE;"
};

using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

foreach (var command in dropCommands)
{
    using var cmd = new NpgsqlCommand(command, connection);
    try
    {
        await cmd.ExecuteNonQueryAsync();
        logger.LogInformation("Executed: {Command}", command);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error executing {Command}", command);
    }
}

logger.LogInformation("Done!");
