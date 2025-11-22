using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using ToDoApp.Components;
using ToDoApp.Data;
using ToDoApp.Entities;
using ToDoApp.Models;
using ToDoApp.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["ConnectionStrings__DefaultConnection"]
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var serverVersion = ServerVersion.Create(new Version(8, 0, 0), ServerType.MySql);

builder.Services.AddDataProtection()
    .SetApplicationName("ToDoApp")
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

// Server-side services for API endpoints
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TaskService>();

// Client-facing services for WebAssembly components
builder.Services.AddScoped<IAuthService, HttpAuthService>();
builder.Services.AddScoped<ITaskService, HttpTaskService>();
builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ReturnUrlParameter = "returnUrl";
        options.SlidingExpiration = true;
        options.Cookie.Name = ".todoapp.auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();
builder.Services.AddAuthorizationCore();

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpClient("ToDoApp.Api", (sp, client) =>
{
    // Hardcoded base URL for API calls; update when moving hosts.
    client.BaseAddress = new Uri("http://148.230.116.159/");
});

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("ToDoApp.Api"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapPost("/auth/login", async Task<IResult> (HttpContext context, AuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var remember = string.Equals(form["RememberMe"], "on", StringComparison.OrdinalIgnoreCase);
    var returnUrl = NormalizeReturnUrl(form["ReturnUrl"].ToString());

    var result = await authService.SignInAsync(email, password, remember);
    if (!result.Succeeded)
    {
        var error = result.Error ?? "Invalid email or password.";
        return Results.Redirect($"/login?error={Uri.EscapeDataString(error)}");
    }

    return Results.Redirect(returnUrl);
}).DisableAntiforgery();

app.MapPost("/auth/register", async Task<IResult> (HttpContext context, AuthService authService) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();
    var confirm = form["ConfirmPassword"].ToString();
    var displayName = form["DisplayName"].ToString().Trim();
    var returnUrl = NormalizeReturnUrl(form["ReturnUrl"].ToString());

    if (!string.Equals(password, confirm, StringComparison.Ordinal))
    {
        return Results.Redirect("/register?error=" + Uri.EscapeDataString("Passwords do not match."));
    }

    var result = await authService.RegisterAsync(email, displayName, password, signInAfterRegister: false);
    if (!result.Succeeded)
    {
        var error = result.Error ?? "Unable to register. Please try again.";
        return Results.Redirect("/register?error=" + Uri.EscapeDataString(error));
    }

    var registeredRedirect = "/login?registered=1&returnUrl=" + Uri.EscapeDataString(returnUrl);
    return Results.Redirect(registeredRedirect);
}).DisableAntiforgery();

app.MapPost("/auth/logout", async Task<IResult> (AuthService authService) =>
{
    await authService.SignOutAsync();
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapPost("/auth/api/login", async Task<IResult> (AuthRequest request, AuthService authService) =>
{
    var result = await authService.SignInAsync(request.Email, request.Password, request.RememberMe);
    if (!result.Succeeded)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Password"] = new[] { result.Error ?? "Invalid credentials." }
        });
    }

    return Results.Ok(new AuthResponse(true, NormalizeReturnUrl(request.ReturnUrl ?? "/")));
}).DisableAntiforgery();

app.MapPost("/auth/api/register", async Task<IResult> (RegisterRequest request, AuthService authService) =>
{
    if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["ConfirmPassword"] = new[] { "Passwords do not match." }
        });
    }

    var result = await authService.RegisterAsync(request.Email, request.DisplayName, request.Password, signInAfterRegister: true);
    if (!result.Succeeded)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Email"] = new[] { result.Error ?? "Unable to register." }
        });
    }

    return Results.Ok(new AuthResponse(true, "/"));
}).DisableAntiforgery();

app.MapPost("/auth/api/logout", async Task<IResult> (AuthService authService) =>
{
    await authService.SignOutAsync();
    return Results.Ok(new AuthResponse(true, "/login"));
}).RequireAuthorization().DisableAntiforgery();

app.MapGet("/auth/me", async Task<IResult> (ClaimsPrincipal user, AuthService authService) =>
{
    var principalId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(principalId))
    {
        return Results.Unauthorized();
    }

    var dbUser = await authService.GetByIdAsync(principalId);
    if (dbUser is null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(ApplicationUserResponse.From(dbUser));
}).RequireAuthorization();

app.MapPut("/auth/profile", async Task<IResult> (ProfileUpdateRequest request, ClaimsPrincipal user, AuthService authService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["User"] = new[] { "User not found." }
        });
    }

    var updated = await authService.UpdateProfileAsync(userId, request.DisplayName, request.Email, request.Theme);
    if (!updated)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Email"] = new[] { "Unable to update profile. Email may already be in use." }
        });
    }

    var dbUser = await authService.GetByIdAsync(userId);
    return Results.Ok(ApplicationUserResponse.From(dbUser!));
}).RequireAuthorization().DisableAntiforgery();

app.MapPut("/auth/notifications", async Task<IResult> (NotificationUpdateRequest request, ClaimsPrincipal user, AuthService authService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["User"] = new[] { "User not found." }
        });
    }

    TimeSpan? digest = null;
    if (!string.IsNullOrWhiteSpace(request.DailyDigestTime) && TimeSpan.TryParse(request.DailyDigestTime, out var parsed))
    {
        digest = parsed;
    }

    var updated = await authService.UpdateNotificationsAsync(userId, request.ReceiveReminders, digest);
    if (!updated)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Notifications"] = new[] { "Unable to update reminders." }
        });
    }

    var dbUser = await authService.GetByIdAsync(userId);
    return Results.Ok(ApplicationUserResponse.From(dbUser!));
}).RequireAuthorization().DisableAntiforgery();

app.MapPut("/auth/password", async Task<IResult> (PasswordUpdateRequest request, ClaimsPrincipal user, AuthService authService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["User"] = new[] { "User not found." }
        });
    }

    if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
    {
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["ConfirmPassword"] = new[] { "New passwords do not match." }
        });
    }

    var result = await authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
    if (!result.Succeeded)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["Password"] = new[] { result.Error ?? "Unable to change password." }
        });
    }

    return Results.Ok(new AuthResponse(true, "/settings"));
}).RequireAuthorization().DisableAntiforgery();

var tasksApi = app.MapGroup("/api/tasks").RequireAuthorization();

tasksApi.MapGet("/", async Task<IResult> (ClaimsPrincipal user, TaskService taskService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    var items = await taskService.GetUpcomingAsync(userId);
    return Results.Ok(items.ToList());
});

tasksApi.MapGet("/{id:int}", async Task<IResult> (int id, ClaimsPrincipal user, TaskService taskService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    var item = await taskService.GetByIdAsync(id, userId);
    return item is not null ? Results.Ok(item) : Results.NotFound();
});

tasksApi.MapPost("/", async Task<IResult> (TaskInputModel model, ClaimsPrincipal user, TaskService taskService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    if (!ValidateModel(model, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var created = await taskService.CreateAsync(model, userId);
    return Results.Created($"/api/tasks/{created.Id}", created);
}).DisableAntiforgery();

tasksApi.MapPut("/{id:int}", async Task<IResult> (int id, TaskInputModel model, ClaimsPrincipal user, TaskService taskService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    if (!ValidateModel(model, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var updated = await taskService.UpdateAsync(id, model, userId);
    return updated ? Results.Ok() : Results.NotFound();
}).DisableAntiforgery();

tasksApi.MapDelete("/{id:int}", async Task<IResult> (int id, ClaimsPrincipal user, TaskService taskService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    var deleted = await taskService.DeleteAsync(id, userId);
    return deleted ? Results.Ok() : Results.NotFound();
}).DisableAntiforgery();

tasksApi.MapPost("/{id:int}/complete", async Task<IResult> (int id, ClaimsPrincipal user, TaskService taskService) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    var completed = await taskService.MarkCompletedAsync(id, userId);
    return completed ? Results.Ok() : Results.NotFound();
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode();

app.Run();

static string NormalizeReturnUrl(string target)
{
    var cleaned = string.IsNullOrWhiteSpace(target) ? "/" : target;
    if (!cleaned.StartsWith('/'))
    {
        cleaned = "/" + cleaned.TrimStart('/');
    }

    return cleaned;
}

static bool ValidateModel(object model, out Dictionary<string, string[]> errors)
{
    var context = new ValidationContext(model);
    var results = new List<ValidationResult>();
    var valid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);
    errors = results
        .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
        .ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Invalid value").ToArray());
    return valid;
}

internal record AuthRequest(string Email, string Password, bool RememberMe, string? ReturnUrl);
internal record RegisterRequest(string Email, string DisplayName, string Password, string ConfirmPassword);
internal record AuthResponse(bool Succeeded, string ReturnUrl);
internal record ProfileUpdateRequest(string DisplayName, string Email, string Theme);
internal record NotificationUpdateRequest(bool ReceiveReminders, string? DailyDigestTime);
internal record PasswordUpdateRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
internal record ApplicationUserResponse(string Id, string Email, string DisplayName, string ThemePreference, bool ReceiveReminders, string? DailyDigestTime)
{
    public static ApplicationUserResponse From(ApplicationUser user) =>
        new(user.Id, user.Email, user.DisplayName, user.ThemePreference, user.ReceiveReminders, user.DailyDigestTime?.ToString(@"hh\:mm"));
}
