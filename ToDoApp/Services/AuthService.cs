using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToDoApp.Data;

namespace ToDoApp.Services;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ILogger<AuthService> _logger = logger;

    public async Task<(bool Succeeded, string? Error)> RegisterAsync(string email, string displayName, string password, bool signInAfterRegister, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var name = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim();

        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            return (false, "An account with that email already exists.");
        }

        var user = new ApplicationUser
        {
            Email = normalizedEmail,
            DisplayName = name,
            PasswordHash = _passwordHasher.HashPassword(password)
        };

        await _dbContext.Users.AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (signInAfterRegister)
        {
            await IssueCookieAsync(user, rememberMe: false);
        }
        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return (false, "Invalid email or password.");
        }

        var passwordValid = _passwordHasher.VerifyHashedPassword(user.PasswordHash, password);
        if (!passwordValid)
        {
            return (false, "Invalid email or password.");
        }

        await IssueCookieAsync(user, rememberMe);
        return (true, null);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            _logger.LogWarning("Attempted to sign out without an active HttpContext.");
            return;
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme, new AuthenticationProperties
        {
            ExpiresUtc = DateTimeOffset.UtcNow
        });
    }

    public async Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await GetByIdAsync(userId, cancellationToken);
    }

    public Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "User not found.");
        }

        if (!_passwordHasher.VerifyHashedPassword(user.PasswordHash, currentPassword))
        {
            return (false, "Current password is incorrect.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await IssueCookieAsync(user, rememberMe: true);
        return (true, null);
    }

    public async Task<bool> UpdateProfileAsync(string userId, string displayName, string email, string theme, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var normalizedEmail = NormalizeEmail(email);
        if (!string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailTaken = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId, cancellationToken);
            if (emailTaken)
            {
                return false;
            }
        }

        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName.Trim();
        user.Email = normalizedEmail;
        user.ThemePreference = string.IsNullOrWhiteSpace(theme) ? "sprout" : theme;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await IssueCookieAsync(user, rememberMe: true);
        return true;
    }

    public async Task<bool> UpdateNotificationsAsync(string userId, bool receiveReminders, TimeSpan? dailyDigestTime, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.ReceiveReminders = receiveReminders;
        user.DailyDigestTime = dailyDigestTime;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task IssueCookieAsync(ApplicationUser user, bool rememberMe)
    {
        var context = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext to sign in.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var expires = rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(12);
        var authProperties = new AuthenticationProperties
        {
            AllowRefresh = true,
            IsPersistent = rememberMe,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(expires)
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }
}
