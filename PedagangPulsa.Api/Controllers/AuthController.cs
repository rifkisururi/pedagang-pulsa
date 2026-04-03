using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;
using System.Data;
using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly IRedisService _redisService;

    public AuthController(
        IConfiguration configuration,
        AppDbContext context,
        ILogger<AuthController> logger,
        IRedisService redisService)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _redisService = redisService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var duplicateCommand = CreateCommand(connection, """
                SELECT "Id", "Username", "Email", "Phone"
                FROM "Users"
                WHERE "Username" = @username OR "Email" = @email OR "Phone" = @phone
                LIMIT 1
                """, transaction))
            {
                AddParameter(duplicateCommand, "@username", request.Username);
                AddParameter(duplicateCommand, "@email", request.Email);
                AddParameter(duplicateCommand, "@phone", request.Phone);

                await using var duplicateReader = await duplicateCommand.ExecuteReaderAsync();
                if (await duplicateReader.ReadAsync())
                {
                    var existingUsername = duplicateReader["Username"] as string;
                    var existingEmail = duplicateReader["Email"] as string;
                    var existingPhone = duplicateReader["Phone"] as string;
                    var fieldName = existingUsername == request.Username ? "Username" :
                                   existingEmail == request.Email ? "Email" : "Phone";

                    return BadRequest(new ErrorResponse
                    {
                        Message = $"{fieldName} already exists",
                        ErrorCode = "DUPLICATE_FIELD"
                    });
                }
            }

            int defaultLevelId;
            string defaultLevelName;
            await using (var levelCommand = CreateCommand(connection, """
                SELECT "Id", "Name"
                FROM "UserLevels"
                WHERE "IsActive" = TRUE
                ORDER BY "Id"
                LIMIT 1
                """, transaction))
            await using (var levelReader = await levelCommand.ExecuteReaderAsync())
            {
                if (!await levelReader.ReadAsync())
                {
                    _logger.LogError("No default user level configured");
                    return StatusCode(500, new ErrorResponse
                    {
                        Message = "System configuration error",
                        ErrorCode = "CONFIG_ERROR"
                    });
                }

                defaultLevelId = levelReader.GetInt32(levelReader.GetOrdinal("Id"));
                defaultLevelName = levelReader.GetString(levelReader.GetOrdinal("Name"));
            }

            var createdAt = DateTime.UtcNow;
            var userId = Guid.NewGuid();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
            var pinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin, workFactor: 12);
            var referralCode = await GenerateUniqueReferralCodeAsync(connection, transaction);

            await using (var insertUserCommand = CreateCommand(connection, """
                INSERT INTO "Users"
                    ("Id", "Username", "FullName", "Email", "Phone", "PasswordHash", "PinHash", "PinFailedAttempts", "LevelId", "ReferralCode", "Status", "CreatedAt", "UpdatedAt")
                VALUES
                    (@id, @username, @fullName, @email, @phone, @passwordHash, @pinHash, @pinFailedAttempts, @levelId, @referralCode, @status, @createdAt, @updatedAt)
                """, transaction))
            {
                AddParameter(insertUserCommand, "@id", userId);
                AddParameter(insertUserCommand, "@username", request.Username);
                AddParameter(insertUserCommand, "@fullName", request.FullName);
                AddParameter(insertUserCommand, "@email", request.Email);
                AddParameter(insertUserCommand, "@phone", request.Phone);
                AddParameter(insertUserCommand, "@passwordHash", passwordHash);
                AddParameter(insertUserCommand, "@pinHash", pinHash);
                AddParameter(insertUserCommand, "@pinFailedAttempts", 0);
                AddParameter(insertUserCommand, "@levelId", defaultLevelId);
                AddParameter(insertUserCommand, "@referralCode", referralCode);
                AddParameter(insertUserCommand, "@status", UserStatus.Active.ToString());
                AddParameter(insertUserCommand, "@createdAt", createdAt);
                AddParameter(insertUserCommand, "@updatedAt", createdAt);
                await insertUserCommand.ExecuteNonQueryAsync();
            }

            await using (var insertBalanceCommand = CreateCommand(connection, """
                INSERT INTO "UserBalances" ("UserId", "ActiveBalance", "HeldBalance", "UpdatedAt")
                VALUES (@userId, @activeBalance, @heldBalance, @updatedAt)
                """, transaction))
            {
                AddParameter(insertBalanceCommand, "@userId", userId);
                AddParameter(insertBalanceCommand, "@activeBalance", 0m);
                AddParameter(insertBalanceCommand, "@heldBalance", 0m);
                AddParameter(insertBalanceCommand, "@updatedAt", createdAt);
                await insertBalanceCommand.ExecuteNonQueryAsync();
            }

            if (!string.IsNullOrWhiteSpace(request.ReferralCode))
            {
                await using var referralCommand = CreateCommand(connection, """
                    SELECT EXISTS (
                        SELECT 1
                        FROM "Users"
                        WHERE "ReferralCode" = @referralCode
                    )
                    """, transaction);
                AddParameter(referralCommand, "@referralCode", request.ReferralCode);
                var referrerExists = Convert.ToBoolean(await referralCommand.ExecuteScalarAsync());

                if (referrerExists)
                {
                    _logger.LogInformation("Referral code {ReferralCode} received during registration for {Username}", request.ReferralCode, request.Username);
                }
            }

            var refreshToken = GenerateRefreshToken();
            await InsertRefreshTokenAsync(connection, transaction, userId, refreshToken, DateTime.UtcNow.AddDays(30));
            await transaction.CommitAsync();

            var response = new RegisterResponse
            {
                Success = true,
                Message = "Registration successful",
                User = new UserDto
                {
                    Id = userId,
                    Username = request.Username,
                    Email = request.Email,
                    FullName = request.FullName,
                    Phone = request.Phone,
                    Level = defaultLevelName,
                    LevelId = defaultLevelId,
                    Balance = 0,
                    ReferralCode = referralCode,
                    CreatedAt = createdAt
                }
            };

            return StatusCode(201, response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during user registration for {Username}", request.Username);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred during registration",
                ErrorCode = "REGISTRATION_ERROR"
            });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var connection = await OpenConnectionAsync();

        await using var loginCommand = CreateCommand(connection, """
            SELECT
                u."Id",
                u."Username",
                u."Email",
                u."FullName",
                u."Phone",
                u."PasswordHash",
                u."LevelId",
                u."Status",
                u."ReferralCode",
                u."CreatedAt",
                COALESCE(ub."ActiveBalance", 0) AS "Balance",
                COALESCE(ul."Name", 'Regular') AS "LevelName"
            FROM "Users" AS u
            LEFT JOIN "UserBalances" AS ub ON ub."UserId" = u."Id"
            LEFT JOIN "UserLevels" AS ul ON ul."Id" = u."LevelId"
            WHERE u."Username" = @username
            LIMIT 1
            """);
        AddParameter(loginCommand, "@username", request.Username);

        await using var loginReader = await loginCommand.ExecuteReaderAsync();
        if (!await loginReader.ReadAsync())
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid username or password",
                ErrorCode = "INVALID_CREDENTIALS"
            });
        }

        var userId = loginReader.GetGuid(loginReader.GetOrdinal("Id"));
        var username = loginReader.GetString(loginReader.GetOrdinal("Username"));
        var email = loginReader["Email"] as string;
        var fullName = loginReader["FullName"] as string;
        var phone = loginReader["Phone"] as string;
        var passwordHash = loginReader["PasswordHash"] as string;
        var levelId = loginReader.GetInt32(loginReader.GetOrdinal("LevelId"));
        var status = loginReader.GetString(loginReader.GetOrdinal("Status"));
        var referralCode = loginReader.GetString(loginReader.GetOrdinal("ReferralCode"));
        var createdAt = loginReader.GetDateTime(loginReader.GetOrdinal("CreatedAt"));
        var balance = loginReader.GetDecimal(loginReader.GetOrdinal("Balance"));
        var levelName = loginReader.GetString(loginReader.GetOrdinal("LevelName"));
        await loginReader.DisposeAsync();

        if (!string.Equals(status, UserStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Account is not active",
                ErrorCode = "ACCOUNT_INACTIVE"
            });
        }

        // Verify password
        if (string.IsNullOrWhiteSpace(passwordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, passwordHash))
        {
            // Log failed login attempt
            _logger.LogWarning("Failed login attempt for user {Username}", request.Username);
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid username or password",
                ErrorCode = "INVALID_CREDENTIALS"
            });
        }

        // Generate tokens
        var accessToken = GenerateJwtToken(userId, username, email, levelId);
        var refreshToken = GenerateRefreshToken();
        await InsertRefreshTokenAsync(connection, null, userId, refreshToken, DateTime.UtcNow.AddDays(30));

        var response = new LoginResponse
        {
            Success = true,
            Message = "Login successful",
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 900, // 15 minutes
            User = new UserDto
            {
                Id = userId,
                Username = username,
                Email = email,
                FullName = fullName,
                Phone = phone,
                Level = levelName,
                LevelId = levelId,
                Balance = balance,
                ReferralCode = referralCode,
                CreatedAt = createdAt
            }
        };

        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var connection = await OpenConnectionAsync();

        await using var refreshCommand = CreateCommand(connection, """
            SELECT
                rt."Id" AS "RefreshTokenId",
                rt."UserId",
                u."Username",
                u."Email",
                u."LevelId",
                u."Status"
            FROM "RefreshTokens" AS rt
            INNER JOIN "Users" AS u ON u."Id" = rt."UserId"
            WHERE rt."Token" = @refreshToken AND rt."IsRevoked" = FALSE AND rt."ExpiresAt" > @now
            LIMIT 1
            """);
        AddParameter(refreshCommand, "@refreshToken", request.RefreshToken);
        AddParameter(refreshCommand, "@now", DateTime.UtcNow);

        await using var refreshReader = await refreshCommand.ExecuteReaderAsync();
        if (!await refreshReader.ReadAsync())
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid or expired refresh token",
                ErrorCode = "INVALID_REFRESH_TOKEN"
            });
        }

        var refreshTokenId = refreshReader.GetGuid(refreshReader.GetOrdinal("RefreshTokenId"));
        var userId = refreshReader.GetGuid(refreshReader.GetOrdinal("UserId"));
        var username = refreshReader.GetString(refreshReader.GetOrdinal("Username"));
        var email = refreshReader["Email"] as string;
        var levelId = refreshReader.GetInt32(refreshReader.GetOrdinal("LevelId"));
        var status = refreshReader.GetString(refreshReader.GetOrdinal("Status"));

        if (!string.Equals(status, UserStatus.Active.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Account is not active",
                ErrorCode = "ACCOUNT_INACTIVE"
            });
        }

        // Generate new tokens
        await refreshReader.DisposeAsync();

        var accessToken = GenerateJwtToken(userId, username, email, levelId);
        var newRefreshToken = GenerateRefreshToken();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var revokeCommand = CreateCommand(connection, """
            UPDATE "RefreshTokens"
            SET "IsRevoked" = TRUE, "RevokedAt" = @revokedAt
            WHERE "Id" = @id
            """, transaction))
        {
            AddParameter(revokeCommand, "@revokedAt", DateTime.UtcNow);
            AddParameter(revokeCommand, "@id", refreshTokenId);
            await revokeCommand.ExecuteNonQueryAsync();
        }

        await InsertRefreshTokenAsync(connection, transaction, userId, newRefreshToken, DateTime.UtcNow.AddDays(30));
        await transaction.CommitAsync();

        var response = new RefreshTokenResponse
        {
            Success = true,
            Message = "Token refreshed successfully",
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 900 // 15 minutes
        };

        return Ok(response);
    }

    [HttpPost("pin/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyPin([FromBody] VerifyPinRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token format",
                ErrorCode = "INVALID_TOKEN_FORMAT"
            });
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "User not found",
                ErrorCode = "USER_NOT_FOUND"
            });
        }

        // Check Redis for lockout
        var lockoutKey = $"pin_lockout:{userId}";
        var isLockedOut = await _redisService.ExistsAsync(lockoutKey);

        if (isLockedOut)
        {
            var ttl = await _redisService.TtlAsync(lockoutKey);
            return StatusCode(429, new ErrorResponse
            {
                Message = $"Too many failed attempts. Account locked for {ttl} seconds",
                ErrorCode = "ACCOUNT_LOCKED"
            });
        }

        // Verify PIN
        if (!BCrypt.Net.BCrypt.Verify(request.Pin, user.PinHash))
        {
            // Increment failed attempts
            user.PinFailedAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.PinFailedAttempts >= 3)
            {
                // Lock for 15 minutes
                user.PinLockedAt = DateTime.UtcNow;
                await _redisService.SetAsync(lockoutKey, "locked", TimeSpan.FromMinutes(15));
                user.PinFailedAttempts = 0; // Reset after lockout

                _logger.LogWarning("User {UserId} locked out due to 3 failed PIN attempts", userId);
            }

            await _context.SaveChangesAsync();

            var remainingAttempts = 3 - user.PinFailedAttempts;
            return Unauthorized(new ErrorResponse
            {
                Message = user.PinFailedAttempts >= 3
                    ? "Account locked for 15 minutes due to too many failed attempts"
                    : $"Invalid PIN. {remainingAttempts} attempts remaining",
                ErrorCode = "INVALID_PIN"
            });
        }

        // Reset failed attempts on successful verification
        user.PinFailedAttempts = 0;
        user.PinLockedAt = null;
        await _context.SaveChangesAsync();

        // Generate PIN session token (store in Redis, TTL 5 min)
        var pinSessionToken = Guid.NewGuid().ToString();
        var sessionKey = $"pin_session:{userId}:{pinSessionToken}";
        await _redisService.SetAsync(sessionKey, userId.ToString(), TimeSpan.FromMinutes(5));

        var response = new VerifyPinResponse
        {
            Success = true,
            Message = "PIN verified successfully",
            PinSessionToken = pinSessionToken,
            ExpiresIn = 300 // 5 minutes
        };

        return Ok(response);
    }

    private string GenerateJwtToken(Guid userId, string username, string? email, int levelId)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        var audience = _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("level_id", levelId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private async Task<string> GenerateUniqueReferralCodeAsync(DbConnection connection, DbTransaction? transaction = null)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed similar looking chars
        var random = new Random();
        var code = string.Empty;

        do
        {
            code = new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            await using var existsCommand = CreateCommand(connection, """
                SELECT EXISTS (
                    SELECT 1
                    FROM "Users"
                    WHERE "ReferralCode" = @referralCode
                )
                """, transaction);
            AddParameter(existsCommand, "@referralCode", code);
            var exists = Convert.ToBoolean(await existsCommand.ExecuteScalarAsync());

            if (!exists)
                break;

        } while (true);

        return code;
    }

    private async Task<DbConnection> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    private static DbCommand CreateCommand(DbConnection connection, string commandText, DbTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Transaction = transaction;
        return command;
    }

    private static void AddParameter(DbCommand command, string parameterName, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task InsertRefreshTokenAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid userId,
        string token,
        DateTime expiresAt)
    {
        await using var command = CreateCommand(connection, """
            INSERT INTO "RefreshTokens" ("Id", "UserId", "Token", "ExpiresAt", "IsRevoked", "CreatedAt")
            VALUES (@id, @userId, @token, @expiresAt, FALSE, @createdAt)
            """, transaction);
        AddParameter(command, "@id", Guid.NewGuid());
        AddParameter(command, "@userId", userId);
        AddParameter(command, "@token", token);
        AddParameter(command, "@expiresAt", expiresAt);
        AddParameter(command, "@createdAt", DateTime.UtcNow);
        await command.ExecuteNonQueryAsync();
    }
}

// Redis Service Interface
public interface IRedisService
{
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task RemoveAsync(string key);
    Task<long> TtlAsync(string key);
}

// Redis Service Implementation (using StackExchange.Redis)
public class RedisService : IRedisService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null)
    {
        // For now, we'll use in-memory implementation
        // TODO: Replace with actual Redis implementation
        _logger.LogDebug("Setting key {Key} in Redis", key);
        await Task.CompletedTask;
    }

    public async Task<string?> GetAsync(string key)
    {
        _logger.LogDebug("Getting key {Key} from Redis", key);
        await Task.CompletedTask;
        return null;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        _logger.LogDebug("Checking if key {Key} exists in Redis", key);
        await Task.CompletedTask;
        return false;
    }

    public async Task RemoveAsync(string key)
    {
        _logger.LogDebug("Removing key {Key} from Redis", key);
        await Task.CompletedTask;
    }

    public async Task<long> TtlAsync(string key)
    {
        _logger.LogDebug("Getting TTL for key {Key} from Redis", key);
        await Task.CompletedTask;
        return 0;
    }
}
