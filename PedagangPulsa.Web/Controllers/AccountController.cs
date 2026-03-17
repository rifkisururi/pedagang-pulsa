using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using PedagangPulsa.Domain.Entities;
using PedagangPulsa.Infrastructure.Data;
using PedagangPulsa.Web.Areas.Admin.ViewModels;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace PedagangPulsa.Web.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        AppDbContext context,
        ILogger<AccountController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // If already authenticated, redirect to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        var model = new LoginViewModel { ReturnUrl = returnUrl };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Find admin user
        var adminUser = await _context.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == model.Username);

        if (adminUser == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Verify password
        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, adminUser.PasswordHash);
        if (!isPasswordValid)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Check if account is active
        if (!adminUser.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Account is disabled. Please contact administrator.");
            return View(model);
        }

        // Update last login
        adminUser.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Create claims
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Name, adminUser.Username),
            new(System.Security.Claims.ClaimTypes.Email, adminUser.Email),
            new(System.Security.Claims.ClaimTypes.Role, adminUser.Role.ToString()),
            new("FullName", adminUser.Username)
        };

        var claimsIdentity = new ClaimsIdentity(claims, "AdminAuth");
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        // Sign in
        await HttpContext.SignInAsync("AdminAuth", claimsPrincipal, new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(8)
        });

        _logger.LogInformation("User {UserName} logged in at {Time}", model.Username, DateTime.UtcNow);

        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminAuth");
        _logger.LogInformation("User logged out at {Time}", DateTime.UtcNow);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(DashboardController.Index), "Dashboard");
    }
}
