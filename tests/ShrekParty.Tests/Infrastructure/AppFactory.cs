using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ShrekParty.Tests.Infrastructure;

/// <summary>
/// Тестовый хост приложения: своя временная папка данных (SQLite-файл и медиа), управляемое время.
/// </summary>
public class AppFactory(string? dataDir = null) : WebApplicationFactory<Program>
{
    public const string AdminLogin = "admin";
    public const string AdminPassword = "admin-test-pass";
    public const string ViewerLogin = "tv";
    public const string ViewerPassword = "tv-test-pass";

    private readonly bool _ownsDataDir = dataDir is null;

    /// <summary>Папка данных этого хоста.</summary>
    public string DataDir { get; } = dataDir ?? Path.Combine(Path.GetTempPath(), "shrek-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Управляемое время приложения.</summary>
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 4, 18, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Storage:DataDir", DataDir);
        builder.UseSetting("Accounts:Admin:Login", AdminLogin);
        builder.UseSetting("Accounts:Admin:Password", AdminPassword);
        builder.UseSetting("Accounts:Viewer:Login", ViewerLogin);
        builder.UseSetting("Accounts:Viewer:Password", ViewerPassword);
        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time)));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDataDir)
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(DataDir, recursive: true);
            }
            catch (IOException)
            {
                // Временную папку не жалко оставить, если файл ещё занят.
            }
        }
    }
}
