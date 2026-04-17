using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Api.DTOs;
using PedagangPulsa.Application.Services;
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
    private readonly AuthService _authService;

    public TransactionController(
        AppDbContext context,
        ILogger<TransactionController> logger,
        AuthService authService)
    {
        _context = context;
        _logger = logger;
        _authService = authService;
    }

    private async Task<decimal> GetBestCostPriceAsync(Guid productId)
    {
        return await _context.SupplierProducts
            .Where(sp => sp.ProductId == productId && sp.IsActive)
            .OrderBy(sp => sp.Seq)
            .Select(sp => sp.CostPrice)
            .FirstOrDefaultAsync();
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
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

        if (!ModelState.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "Validation failed",
                Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
            });
        }

        // X-Reference-Id is required for idempotency
        var referenceId = Request.Headers["X-Reference-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return BadRequest(new ErrorResponse
            {
                Message = "X-Reference-Id header is required",
                ErrorCode = "REFERENCE_ID_REQUIRED"
            });
        }

        // Check idempotency key first (before consuming PIN session)
        var existingKey = await _context.IdempotencyKeys.FindAsync(userId, referenceId);
        if (existingKey != null)
        {
            if (existingKey.ExpiresAt > DateTime.UtcNow && !string.IsNullOrEmpty(existingKey.ResponseCache))
            {
                return Ok(System.Text.Json.JsonSerializer.Deserialize<object>(existingKey.ResponseCache));
            }
        }

        // Validate and atomically consume PIN session (prevents race condition)
        var pinValidation = await _authService.ValidateAndConsumePinSessionAsync(userId, request.PinSessionToken);
        if (!pinValidation.IsValid)
        {
            return Unauthorized(new ErrorResponse
            {
                Message = pinValidation.ErrorMessage,
                ErrorCode = "INVALID_PIN_SESSION"
            });
        }

        try
        {
            var user = await _context.Users
                .Include(u => u.Balance)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Balance == null)
            {
                return NotFound(new ErrorResponse
                {
                    Message = "User not found",
                    ErrorCode = "USER_NOT_FOUND"
                });
            }

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

            var levelPrice = product.ProductLevelPrices
                .FirstOrDefault(plp => plp.LevelId == user.LevelId && plp.IsActive);

            if (levelPrice == null || levelPrice.Margin < 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Product not available for your level",
                    ErrorCode = "PRODUCT_NOT_AVAILABLE"
                });
            }

            var costPrice = await GetBestCostPriceAsync(request.ProductId);

            if (costPrice <= 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Product not available (no supplier mapped)",
                    ErrorCode = "PRODUCT_NOT_AVAILABLE"
                });
            }

            var sellPrice = costPrice + levelPrice.Margin;

            if (user.Balance.ActiveBalance < sellPrice)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Insufficient balance",
                    ErrorCode = "INSUFFICIENT_BALANCE"
                });
            }

            var heldBalanceBefore = user.Balance.HeldBalance;
            var activeBalanceBefore = user.Balance.ActiveBalance;

            user.Balance.ActiveBalance -= sellPrice;
            user.Balance.HeldBalance += sellPrice;

            var holdLedger = new BalanceLedger
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                UserId = userId,
                Type = BalanceTransactionType.PurchaseHold,
                Amount = -sellPrice,
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

            var referenceIdGen = $"{userId}{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                ReferenceId = referenceIdGen,
                UserId = userId,
                ProductId = request.ProductId,
                Destination = request.DestinationNumber,
                SellPrice = sellPrice,
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
                SellPrice = sellPrice,
                CreatedAt = transaction.CreatedAt
            };

            // Store idempotency key with transaction reference
            if (existingKey != null)
            {
                existingKey.ResponseCache = System.Text.Json.JsonSerializer.Serialize(response);
                existingKey.TransactionId = transaction.Id;
                await _context.SaveChangesAsync();
            }
            else
            {
                var newKey = new IdempotencyKey
                {
                    UserId = userId,
                    Key = referenceId,
                    TransactionId = transaction.Id,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    ResponseCache = System.Text.Json.JsonSerializer.Serialize(response)
                };
                _context.IdempotencyKeys.Add(newKey);
                await _context.SaveChangesAsync();
            }

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
            _logger.LogError(ex, "Error creating transaction for user {UserId}", userId);
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

        var transaction = await _context.Transactions
            .Include(t => t.Product)
            .Where(t => t.ReferenceId == referenceId && t.UserId == userId)
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

        var query = _context.Transactions
            .Include(t => t.Product)
            .Where(t => t.UserId == userId);

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
