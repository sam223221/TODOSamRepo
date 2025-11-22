using System.IO;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using ToDoApp.Components;
using ToDoApp.Data;
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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.MapPost("/auth/login", async Task<IResult> (HttpContext context, IAuthService authService) =>
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

app.MapPost("/auth/register", async Task<IResult> (HttpContext context, IAuthService authService) =>
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

app.MapPost("/auth/logout", async Task<IResult> (IAuthService authService) =>
{
    await authService.SignOutAsync();
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

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
