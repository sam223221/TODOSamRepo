using System.Security.Claims;
using ToDoApp.Data;

namespace ToDoApp.Services;

public interface IAuthService
{
    Task<(bool Succeeded, string? Error)> RegisterAsync(string email, string displayName, string password, bool signInAfterRegister, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task<bool> UpdateProfileAsync(string userId, string displayName, string email, string theme, CancellationToken cancellationToken = default);

    Task<bool> UpdateNotificationsAsync(string userId, bool receiveReminders, TimeSpan? dailyDigestTime, CancellationToken cancellationToken = default);
}
