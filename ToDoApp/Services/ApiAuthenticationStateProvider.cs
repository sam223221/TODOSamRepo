using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace ToDoApp.Services;

public sealed class ApiAuthenticationStateProvider(HttpClient httpClient, ILogger<ApiAuthenticationStateProvider> logger)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<ApiAuthenticationStateProvider> _logger = logger;
    private ApplicationUserResponse? _cachedUser;
    private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_cachedUser is null || now - _lastFetch > CacheDuration)
            {
                _cachedUser = await _httpClient.GetFromJsonAsync<ApplicationUserResponse>("/auth/me");
                _lastFetch = now;
            }

            if (_cachedUser is null)
            {
                return Anonymous;
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _cachedUser.Id),
                new Claim(ClaimTypes.Email, _cachedUser.Email),
                new Claim(ClaimTypes.Name, _cachedUser.DisplayName)
            }, HeaderNames.Cookie);

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to resolve authentication state from /auth/me.");
            _cachedUser = null;
            return Anonymous;
        }
    }

    public void NotifyAuthenticationStateChangedExternally()
    {
        _lastFetch = DateTimeOffset.MinValue;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
