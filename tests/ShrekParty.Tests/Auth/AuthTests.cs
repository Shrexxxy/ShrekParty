using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Auth;

public class AuthTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Admin_from_config_can_log_in()
    {
        var client = TestClients.Anonymous(_factory);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin", password = "admin-test-pass" });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await login.Content.ReadFromJsonAsync<MeDto>(TestClients.Json);
        me!.Role.Should().Be(UserRole.Admin);
        var again = await client.GetFromJsonAsync<MeDto>("/api/auth/me", TestClients.Json);
        again!.Id.Should().Be(me.Id);
    }

    [Fact]
    public async Task Viewer_from_config_can_log_in()
    {
        var client = await TestClients.ViewerAsync(_factory);

        var me = await client.GetFromJsonAsync<MeDto>("/api/auth/me", TestClients.Json);

        me!.Role.Should().Be(UserRole.Viewer);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var client = TestClients.Anonymous(_factory);

        var response = await client.PostAsJsonAsync("/api/auth/login", new { login = "admin", password = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_without_cookie_returns_401()
    {
        var client = TestClients.Anonymous(_factory);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        using (var first = new AppFactory(_factory.DataDir))
        {
            first.CreateClient();
        }

        using var second = new AppFactory(_factory.DataDir);
        second.CreateClient();

        using var scope = second.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.CountAsync(u => u.Role == UserRole.Admin)).Should().Be(1);
    }

    [Fact]
    public async Task Cookie_keys_are_stored_in_data_dir()
    {
        // В контейнере всё, кроме /data, теряется при передеплое — ключи cookie должны жить рядом с базой.
        await TestClients.AdminAsync(_factory);

        Directory.GetFiles(Path.Combine(_factory.DataDir, "keys"), "*.xml").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_survives_restart_with_same_data_dir()
    {
        string cookie;
        using (var first = new AppFactory(_factory.DataDir))
        {
            cookie = await AuthCookies.LoginAsync(first, AppFactory.AdminLogin, AppFactory.AdminPassword);
        }

        using var second = new AppFactory(_factory.DataDir);
        var client = second.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_clears_session()
    {
        var client = await TestClients.AdminAsync(_factory);

        var logout = await client.PostAsync("/api/auth/logout", null);
        var me = await client.GetAsync("/api/auth/me");

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _factory.Dispose();
}
