using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ToDoApp.Data;

namespace ToDoApp.Services;

public sealed class HttpAuthService(HttpClient httpClient, AuthenticationStateProvider authStateProvider, ILogger<HttpAuthService> logger) : IAuthService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ApiAuthenticationStateProvider _stateProvider = authStateProvider as ApiAuthenticationStateProvider
        ?? throw new InvalidOperationException("ApiAuthenticationStateProvider is required for WebAssembly auth.");
    private readonly ILogger<HttpAuthService> _logger = logger;

    public async Task<(bool Succeeded, string? Error)> RegisterAsync(string email, string displayName, string password, bool signInAfterRegister, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/auth/api/register", new RegisterRequest(email, displayName, password, password), cancellationToken);
        return await HandleAuthResponseAsync(response);
    }

    public async Task<(bool Succeeded, string? Error)> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/auth/api/login", new AuthRequest(email, password, rememberMe, "/"), cancellationToken);
        return await HandleAuthResponseAsync(response);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _httpClient.PostAsync("/auth/api/logout", content: null, cancellationToken);
        _stateProvider.NotifyAuthenticationStateChangedExternally();
    }

    public async Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var user = await FetchUserAsync(cancellationToken);
        return user is null ? null : ToApplicationUser(user);
    }

    public async Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Current API only exposes the current user; ignore mismatched IDs to avoid leaking data.
        var user = await FetchUserAsync(cancellationToken);
        if (user is null || !string.Equals(user.Id, userId, StringComparison.Ordinal))
        {
            return null;
        }

        return ToApplicationUser(user);
    }

    public async Task<(bool Succeeded, string? Error)> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("/auth/password", new PasswordUpdateRequest(currentPassword, newPassword, newPassword), cancellationToken);
        return await HandleAuthResponseAsync(response);
    }

    public async Task<bool> UpdateProfileAsync(string userId, string displayName, string email, string theme, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("/auth/profile", new ProfileUpdateRequest(displayName, email, theme), cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            _stateProvider.NotifyAuthenticationStateChangedExternally();
            return true;
        }

        LogErrors(response, "profile");
        return false;
    }

    public async Task<bool> UpdateNotificationsAsync(string userId, bool receiveReminders, TimeSpan? dailyDigestTime, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("/auth/notifications", new NotificationUpdateRequest(receiveReminders, dailyDigestTime?.ToString(@"hh\:mm")), cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        LogErrors(response, "notifications");
        return false;
    }

    private async Task<ApplicationUserResponse?> FetchUserAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ApplicationUserResponse>("/auth/me", cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch current user.");
            return null;
        }
    }

    private async Task<(bool Succeeded, string? Error)> HandleAuthResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            _stateProvider.NotifyAuthenticationStateChangedExternally();
            return (true, null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>() ?? new ValidationProblemDetails();
        var error = problem.Errors?.SelectMany(kvp => kvp.Value).FirstOrDefault();
        _logger.LogWarning("Auth API call failed with status {StatusCode}: {Error}", response.StatusCode, error);
        return (false, error ?? "Request failed.");
    }

    private void LogErrors(HttpResponseMessage response, string operation)
    {
        _ = response.Content.ReadAsStringAsync().ContinueWith(task =>
        {
            var body = task.IsCompletedSuccessfully ? task.Result : "<unavailable>";
            _logger.LogWarning("Auth API {Operation} failed ({StatusCode}): {Body}", operation, response.StatusCode, body);
        });
    }

    private static ApplicationUser ToApplicationUser(ApplicationUserResponse response)
    {
        return new ApplicationUser
        {
            Id = response.Id,
            Email = response.Email,
            DisplayName = response.DisplayName,
            ThemePreference = response.ThemePreference,
            ReceiveReminders = response.ReceiveReminders,
            DailyDigestTime = string.IsNullOrWhiteSpace(response.DailyDigestTime) ? null : TimeSpan.Parse(response.DailyDigestTime)
        };
    }
}
