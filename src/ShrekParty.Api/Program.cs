using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShrekParty.Api.Admin;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;
using ShrekParty.Api.Registration;
using ShrekParty.Api.Spa;
using ShrekParty.Api.Tv;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
builder.Services.Configure<AccountsOptions>(builder.Configuration.GetSection(AccountsOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    Directory.CreateDirectory(storage.DataDir);
    options.UseSqlite($"Data Source={storage.DatabasePath}");
});
builder.Services.AddScoped<StaffSeeder>();
builder.Services.AddScoped<DonkeyService>();
builder.Services.AddSingleton<ILiveNotifier, LiveNotifier>();
builder.Services.AddScoped<SnapshotBuilder>();
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddSingleton<IPasswordAnalyzer, ZxcvbnPasswordAnalyzer>();
builder.Services.AddSingleton<IPasswordHasher<RegistrationAttempt>, PasswordHasher<RegistrationAttempt>>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<MediaStorage>();
builder.Services.AddScoped<TvFactsService>();
builder.Services.AddScoped<ResetService>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddShrekAuth();
builder.Services.AddShrekProxy();
builder.Services.AddDataProtection()
    .SetApplicationName("ShrekParty")
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration.GetSection(StorageOptions.Section).Get<StorageOptions>()?.KeysDir ?? new StorageOptions().KeysDir));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<StaffSeeder>().SeedAsync();
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();
app.MapLeaderboard();
app.MapAdmin();
app.MapRegistration();
app.MapMedia();
app.MapTv();
app.MapHub<LiveHub>("/hub");
app.MapSpa();

app.Run();

/// <summary>
/// Точка входа; partial — для тестового хоста.
/// </summary>
public partial class Program;
