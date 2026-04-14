using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Infrastructure.Data;
using System.Security.Claims;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionController> _logger;
    private readonly IRedisService _redisService;

    public TransactionController(
        AppDbContext context,
        ILogger<TransactionController> logger,
        IRedisService redisService)
    {
        _context = context;
        _logger = logger;
        _redisService = redisService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        // Validate PIN session token
        var sessionKey = $"pin_session:{userGuid}:{request.PinSessionToken}";
        var sessionValue = await _redisService.GetAsync(sessionKey);

        if (sessionValue == null || sessionValue != userGuid.ToString())
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid or expired PIN session. Please verify your PIN again.",
                ErrorCode = "INVALID_PIN_SESSION"
            });
        }

        // Check idempotency
        var referenceId = Request.Headers["X-Reference-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            var existingKey = await _context.IdempotencyKeys.FindAsync(referenceId, userGuid);
            if (existingKey != null)
            {
                if (existingKey.ExpiresAt > DateTime.UtcNow && !string.IsNullOrEmpty(existingKey.ResponseCache))
                {
                    return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(existingKey.ResponseCache));
                }
            }
        }

        try
        {
            // Get user with balance
            var user = await _context.Users
                .Include(u => u.Balance)
                .FirstOrDefaultAsync(u => u.Id == userGuid);

            if (user?.Balance == null)
            {
                return NotFound(new ErrorResponse
                {
                    Message = "User not found",
                    ErrorCode = "USER_NOT_FOUND"
                });
            }

            // Get product with pricing for user's level
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductLevelPrices)
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive);

            if (product == null)
            {
                return NotFound(new ErrorResponse
                {
                    Message = "Product not found",
                    ErrorCode = "PRODUCT_NOT_FOUND"
                });
            }

            // Get price for user's level
            var levelPrice = product.ProductLevelPrices
                .FirstOrDefault(plp => plp.LevelId == user.LevelId && plp.IsActive);

            if (levelPrice == null || levelPrice.SellPrice <= 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Product not available for your level",
                    ErrorCode = "PRODUCT_NOT_AVAILABLE"
                });
            }

            // Check balance
            if (user.Balance.ActiveBalance < levelPrice.SellPrice)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Insufficient balance",
                    ErrorCode = "INSUFFICIENT_BALANCE"
                });
            }

            // Hold balance
            var heldBalanceBefore = user.Balance.HeldBalance;
            var activeBalanceBefore = user.Balance.ActiveBalance;

            user.Balance.ActiveBalance -= levelPrice.SellPrice;
            user.Balance.HeldBalance += levelPrice.SellPrice;

            // Create ledger entry for hold
            var holdLedger = new BalanceLedger
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = userGuid,
                Type = BalanceTransactionType.PurchaseHold,
                Amount = -levelPrice.SellPrice,
                ActiveBefore = activeBalanceBefore,
                ActiveAfter = user.Balance.ActiveBalance,
                HeldBefore = heldBalanceBefore,
                HeldAfter = user.Balance.HeldBalance,
                RefType = "Transaction",
                Notes = $"Hold for {product.Name} - {request.DestinationNumber}",
                CreatedAt = DateTime.UtcNow
            };
            _context.BalanceLedgers.Add(holdLedger);

            await _context.SaveChangesAsync();

            // Create transaction
            var referenceIdGen = System.Security.Cryptography.RandomNumberGenerator.GetString("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 8);
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ReferenceId = referenceIdGen,
                UserId = userGuid,
                ProductId = request.ProductId,
                Destination = request.DestinationNumber,
                SellPrice = levelPrice.SellPrice,
                CostPrice = null,
                Status = TransactionStatus.Pending,
                PinVerifiedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            var response = new TransactionResponse
            {
                Success = true,
                Message = "Transaction created successfully",
                ReferenceId = transaction.ReferenceId,
                Status = transaction.Status.ToString().ToLower(),
                Product = new ProductInfo
                {
                    Name = product.Name,
                    Code = product.Code,
                    Operator = product.Operator,
                    Denomination = product.Denomination
                },
                Destination = transaction.Destination,
                SellPrice = levelPrice.SellPrice,
                CreatedAt = transaction.CreatedAt
            };

            // Cache response for idempotency
            if (!string.IsNullOrWhiteSpace(referenceId))
            {
                var key = await _context.IdempotencyKeys.FindAsync(referenceId, userGuid);
                if (key != null)
                {
                    key.ResponseCache = System.Text.Json.JsonSerializer.Serialize(response);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    var newKey = new IdempotencyKey
                    {
                        UserId = userGuid,
                        Key = referenceId,
                        ExpiresAt = DateTime.UtcNow.AddHours(24),
                        ResponseCache = System.Text.Json.JsonSerializer.Serialize(response)
                    };
                    _context.IdempotencyKeys.Add(newKey);
                    await _context.SaveChangesAsync();
                }
            }

            // Consume PIN session token (one-time use)
            await _redisService.RemoveAsync(sessionKey);

            return StatusCode(201, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse
            {
                Message = ex.Message,
                ErrorCode = "VALIDATION_ERROR"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction for user {UserId}", userGuid);
            return StatusCode(500, new ErrorResponse
            {
                Message = "An error occurred while processing your transaction",
                ErrorCode = "TRANSACTION_ERROR"
            });
        }
    }

    [HttpGet("{referenceId}")]
    public async Task<IActionResult> GetTransaction(string referenceId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        var transaction = await _context.Transactions
            .Include(t => t.Product)
            .Where(t => t.ReferenceId == referenceId && t.UserId == userGuid)
            .Select(t => new TransactionDetailItem
            {
                ReferenceId = t.ReferenceId,
                Status = t.Status.ToString().ToLower(),
                ProductName = t.Product!.Name,
                ProductCode = t.Product.Code,
                CategoryName = t.Product.Category!.Name,
                Destination = t.Destination,
                SellPrice = t.SellPrice,
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (transaction == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Transaction not found",
                ErrorCode = "TRANSACTION_NOT_FOUND"
            });
        }

        return Ok(new
        {
            success = true,
            data = transaction
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = "Invalid token",
                ErrorCode = "INVALID_TOKEN"
            });
        }

        var userGuid = Guid.Parse(userId);

        var query = _context.Transactions
            .Include(t => t.Product)
            .Where(t => t.UserId == userGuid);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransactionStatus>(status, true, out var statusEnum))
        {
            query = query.Where(t => t.Status == statusEnum);
        }

        var totalRecords = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionDetailItem
            {
                ReferenceId = t.ReferenceId,
                Status = t.Status.ToString().ToLower(),
                ProductName = t.Product!.Name,
                ProductCode = t.Product.Code,
                CategoryName = t.Product.Category!.Name,
                Destination = t.Destination,
                SellPrice = t.SellPrice,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(new TransactionListResponse
        {
            Success = true,
            Data = transactions,
            TotalRecords = totalRecords,
            Page = page,
            PageSize = pageSize
        });
    }
}
