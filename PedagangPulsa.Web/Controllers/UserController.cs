using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Domain.Enums;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly UserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(UserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new UserDetailViewModel
        {
            Id = user.Id,
            Username = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Level = user.Level?.Name ?? "Unknown",
            Status = user.Status.ToString(),
            ActiveBalance = user.Balance?.ActiveBalance ?? 0,
            HeldBalance = user.Balance?.HeldBalance ?? 0,
            CreatedAt = user.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            ReferralCode = user.ReferralCode,
            ReferredBy = user.Referrer?.UserName,
            PinLockedAt = user.PinLockedAt
        };

        // Map balance ledger
        model.RecentTransactions = user.BalanceLedgers.Select(bl => new UserDetailViewModel.BalanceLedgerItem
        {
            CreatedAt = bl.CreatedAt,
            Type = bl.Type.ToString(),
            Amount = bl.Amount,
            ActiveBefore = bl.ActiveBefore,
            ActiveAfter = bl.ActiveAfter,
            Notes = bl.Notes
        }).ToList();

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> EditLevel(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var allLevels = await _userService.GetAllLevelsAsync();

        var model = new EditLevelUserViewModel
        {
            UserId = user.Id,
            Username = user.UserName,
            FullName = user.FullName,
            CurrentLevelId = user.LevelId,
            CurrentLevelName = user.Level?.Name ?? "Unknown",
            AvailableLevels = allLevels.Select(l => new EditLevelUserViewModel.UserLevelItem
            {
                Id = l.Id,
                Name = l.Name
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLevel(EditLevelUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.UpdateUserLevelAsync(model.UserId, model.NewLevelId, User.Identity?.Name, null);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to update user level. User not found.");
            return View(model);
        }

        TempData["Success"] = "User level updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new SuspendUserViewModel
        {
            UserId = user.Id,
            Username = user.UserName,
            FullName = user.FullName,
            IsSuspended = user.Status == Domain.Enums.UserStatus.Suspended
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(SuspendUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        bool result;
        if (model.IsSuspended)
        {
            // Unsuspend
            result = await _userService.UnsuspendUserAsync(model.UserId, User.Identity?.Name);
            if (result)
            {
                TempData["Success"] = "User unsuspended successfully.";
            }
        }
        else
        {
            // Suspend
            result = await _userService.SuspendUserAsync(model.UserId, model.Reason, User.Identity?.Name);
            if (result)
            {
                TempData["Success"] = "User suspended successfully.";
            }
        }

        if (!result)
        {
            ModelState.AddModelError("", "Failed to update user status. User not found.");
            return View(model);
        }

        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    #region User Management Actions

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockPin(Guid id)
    {
        var result = await _userService.UnblockPinAsync(id);
        if (!result)
        {
            return Json(new { success = false, message = "User not found." });
        }
        return Json(new { success = true, message = "PIN unblocked successfully." });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new ResetPasswordViewModel
        {
            UserId = user.Id,
            Username = user.UserName
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.ResetPasswordAsync(model.UserId, model.NewPassword);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to reset password. User not found.");
            return View(model);
        }

        TempData["Success"] = "Password reset successfully.";
        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> ResetPin(Guid id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var model = new ResetPinViewModel
        {
            UserId = user.Id,
            Username = user.UserName
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPin(ResetPinViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.ResetPinAsync(model.UserId, model.NewPin);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to reset PIN. User not found.");
            return View(model);
        }

        TempData["Success"] = "PIN reset successfully.";
        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid id, UserStatus status)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return Json(new { success = false, message = "User not found." });
        }

        // Prevent toggling Suspended status here (use Suspend page instead)
        if (status == UserStatus.Suspended)
        {
            return Json(new { success = false, message = "Use Suspend action instead." });
        }

        var result = await _userService.SetUserStatusAsync(id, status);
        if (!result)
        {
            return Json(new { success = false, message = "Failed to update user status." });
        }

        var label = status == UserStatus.Active ? "activated" : "deactivated";
        return Json(new { success = true, message = $"User {label} successfully." });
    }

    #endregion

    #region AJAX Endpoints for DataTables

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] int? levelId = null,
        [FromForm] string? status = null,
        [FromForm] string? startDate = null,
        [FromForm] string? endDate = null,
        [FromForm] string? orderColumn = null,
        [FromForm] string? orderDirection = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        DateTime? startDt = null;
        DateTime? endDt = null;

        if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var parsedStart))
        {
            startDt = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(endDate) && DateTime.TryParse(endDate, out var parsedEnd))
        {
            endDt = parsedEnd.AddDays(1).AddTicks(-1); // End of day
        }

        var (users, totalFiltered, totalRecords) = await _userService.GetUsersPagedAsync(
            page,
            pageSize,
            search,
            levelId,
            status,
            startDt,
            endDt,
            orderColumn,
            orderDirection);

        var userData = users.Select(u => new UserListViewModel.UserDataRow
        {
            Id = u.Id,
            Username = u.UserName,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Level = u.Level?.Name ?? "Unknown",
            Status = u.Status.ToString(),
            Balance = u.Balance?.ActiveBalance ?? 0,
            CreatedAt = u.CreatedAt.ToString("dd MMM yyyy")
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = userData
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetLevels()
    {
        var levels = await _userService.GetAllLevelsAsync();
        var result = levels.Select(l => new
        {
            id = l.Id,
            name = l.Name
        });
        return Json(result);
    }

    #endregion
}
