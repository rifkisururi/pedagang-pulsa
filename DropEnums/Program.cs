using System;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "Host=localhost;Database=pedagangpulsa;Username=postgres;Password=postgres";

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
        Console.WriteLine($"Executed: {command}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

Console.WriteLine("Done!");
