using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Auth;

/// <summary>
/// Вход, выход, текущий пользователь.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Политика: только админ.</summary>
    public const string AdminPolicy = "Admin";

    /// <summary>Политика: ТВ или админ.</summary>
    public const string ViewerPolicy = "Viewer";

    /// <summary>Политика: только игрок.</summary>
    public const string PlayerPolicy = "Player";

    /// <summary>
    /// Регистрирует cookie-авторизацию и политики.
    /// </summary>
    public static IServiceCollection AddShrekAuth(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "shrek.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                // API, а не страницы: вместо редиректа на логин — коды ответа.
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, p => p.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(ViewerPolicy, p => p.RequireRole(nameof(UserRole.Viewer), nameof(UserRole.Admin)))
            .AddPolicy(PlayerPolicy, p => p.RequireRole(nameof(UserRole.Player)));

        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        return services;
    }

    /// <summary>
    /// Входит в систему от имени пользователя (ставит cookie).
    /// </summary>
    public static Task SignInAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    /// <summary>
    /// Id текущего пользователя из cookie.
    /// </summary>
    public static Guid? UserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Собирает DTO текущего пользователя.
    /// </summary>
    public static MeDto ToMe(this User user) =>
        new(user.Id, user.Login, user.Role, user.AvatarMediaId is { } media ? MediaStorage.UrlFor(media) : null);

    /// <summary>
    /// Маршруты <c>/api/auth</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, AppDbContext db, IPasswordHasher<User> hasher, HttpContext http) =>
        {
            var normalized = User.Normalize(request.Login ?? "");
            var user = await db.Users.SingleOrDefaultAsync(u => u.LoginNormalized == normalized);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "") == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            await SignInAsync(http, user);
            return Results.Ok(user.ToMe());
        });

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var user = principal.UserId() is { } id ? await db.Users.FindAsync(id) : null;
            return user is null ? Results.Unauthorized() : Results.Ok(user.ToMe());
        }).RequireAuthorization();

        return app;
    }
}
