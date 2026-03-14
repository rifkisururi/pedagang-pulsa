using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Application.Services;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Web.Areas.Admin.ViewModels;

namespace PedagangPulsa.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class UserLevelController : Controller
{
    private readonly UserLevelService _userLevelService;
    private readonly ILogger<UserLevelController> _logger;

    public UserLevelController(UserLevelService userLevelService, ILogger<UserLevelController> logger)
    {
        _userLevelService = userLevelService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var level = await _userLevelService.GetLevelByIdAsync(id);
        if (level == null)
        {
            return NotFound();
        }

        var model = new UserLevelDetailViewModel
        {
            Id = level.Id,
            Name = level.Name,
            MarkupType = level.MarkupType.ToString(),
            MarkupValue = level.MarkupValue,
            Description = level.Description,
            IsActive = level.IsActive,
            CreatedAt = level.CreatedAt,
            UpdatedAt = level.UpdatedAt
        };

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserLevelViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var level = new UserLevel
        {
            Name = model.Name,
            MarkupType = model.MarkupType,
            MarkupValue = model.MarkupValue,
            Description = model.Description,
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userLevelService.CreateLevelAsync(level);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to create level. Level with this name may already exist.");
            return View(model);
        }

        TempData["Success"] = "User level created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Edit(int id)
    {
        var level = await _userLevelService.GetLevelByIdAsync(id);
        if (level == null)
        {
            return NotFound();
        }

        var model = new UserLevelViewModel
        {
            Id = level.Id,
            Name = level.Name,
            MarkupType = level.MarkupType,
            MarkupValue = level.MarkupValue,
            Description = level.Description,
            IsActive = level.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserLevelViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var level = new UserLevel
        {
            Id = model.Id.Value,
            Name = model.Name,
            MarkupType = model.MarkupType,
            MarkupValue = model.MarkupValue,
            Description = model.Description,
            IsActive = model.IsActive
        };

        var result = await _userLevelService.UpdateLevelAsync(level);
        if (result == null)
        {
            ModelState.AddModelError("", "Failed to update level. Level with this name may already exist.");
            return View(model);
        }

        TempData["Success"] = "User level updated successfully.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var level = await _userLevelService.GetLevelByIdAsync(id);
        if (level == null)
        {
            return NotFound();
        }

        var model = new UserLevelDeleteViewModel
        {
            Id = level.Id,
            Name = level.Name
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(UserLevelDeleteViewModel model)
    {
        var result = await _userLevelService.DeleteLevelAsync(model.Id);
        if (!result)
        {
            ModelState.AddModelError("", "Failed to delete level. Level may be in use by existing users.");
            return View(model);
        }

        TempData["Success"] = "User level deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    #region AJAX Endpoints

    [HttpPost]
    public async Task<IActionResult> GetData(
        [FromForm] int draw,
        [FromForm] int start,
        [FromForm] int length,
        [FromForm] string? search = null,
        [FromForm] string? isActive = null)
    {
        var page = (start / length) + 1;
        var pageSize = length;

        bool? activeFilter = null;
        if (!string.IsNullOrWhiteSpace(isActive) && bool.TryParse(isActive, out var activeBool))
        {
            activeFilter = activeBool;
        }

        var (levels, totalFiltered, totalRecords) = await _userLevelService.GetLevelsPagedAsync(
            page,
            pageSize,
            search,
            activeFilter);

        var levelData = levels.Select(l => new UserLevelListViewModel.LevelDataRow
        {
            Id = l.Id,
            Name = l.Name,
            MarkupType = l.MarkupType.ToString(),
            MarkupValue = l.MarkupValue,
            IsActive = l.IsActive ? "Active" : "Inactive",
            UserCount = 0 // Will be populated from actual user count
        }).ToList();

        return Json(new
        {
            draw = draw,
            recordsTotal = totalRecords,
            recordsFiltered = totalFiltered,
            data = levelData
        });
    }

    #endregion
}
