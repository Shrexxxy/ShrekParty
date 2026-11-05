using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShrekParty.Api.Admin;
using ShrekParty.Api.Auction;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Bosses;
using ShrekParty.Api.Ceremony;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Days;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.HideAndSeek;
using ShrekParty.Api.Laugh;
using ShrekParty.Api.Live;
using ShrekParty.Api.Mafia;
using ShrekParty.Api.Media;
using ShrekParty.Api.Neuro;
using ShrekParty.Api.Pets;
using ShrekParty.Api.PhotoHunt;
using ShrekParty.Api.Quiz;
using ShrekParty.Api.Reaction;
using ShrekParty.Api.Registration;
using ShrekParty.Api.Spa;
using ShrekParty.Api.Tapper;
using ShrekParty.Api.Tote;
using ShrekParty.Api.Tournament;
using ShrekParty.Api.Tv;

var builder = WebApplication.CreateBuilder(args);

var tempPath = builder.Configuration["ASPNETCORE_TEMP"]
               ?? Path.Combine(AppContext.BaseDirectory, "temp");

Directory.CreateDirectory(tempPath);

Environment.SetEnvironmentVariable("ASPNETCORE_TEMP", tempPath);

Console.WriteLine($"ASP.NET Core TEMP: {tempPath}");

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
builder.Services.Configure<AccountsOptions>(builder.Configuration.GetSection(AccountsOptions.Section));
builder.Services.Configure<PartyOptions>(builder.Configuration.GetSection(PartyOptions.Section));
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
builder.Services.AddScoped<EventEngine>();
builder.Services.AddHostedService<EventTicker>();
builder.Services.AddSingleton(Random.Shared);
builder.Services.AddScoped<IEventModule, TournamentModule>();
builder.Services.AddScoped<TournamentService>();
builder.Services.AddScoped<ToteService>();
builder.Services.AddScoped<LaughModule>();
builder.Services.AddScoped<IEventModule>(sp => sp.GetRequiredService<LaughModule>());
builder.Services.AddScoped<HideSeekModule>();
builder.Services.AddScoped<IEventModule>(sp => sp.GetRequiredService<HideSeekModule>());
builder.Services.AddScoped<HideSeekService>();
builder.Services.AddScoped<IEventModule, PhotoHuntModule>();
builder.Services.AddScoped<PhotoHuntService>();
builder.Services.AddScoped<TvContentService>();
builder.Services.AddScoped<TvSlidesService>();
builder.Services.AddScoped<CeremonyModule>();
builder.Services.AddScoped<IEventModule>(sp => sp.GetRequiredService<CeremonyModule>());
builder.Services.AddScoped<IEventModule, ReactionModule>();
builder.Services.AddScoped<ReactionService>();
builder.Services.AddScoped<IEventModule, TapperModule>();
builder.Services.AddScoped<TapperService>();
builder.Services.AddScoped<IEventModule, DaysModule>();
builder.Services.AddScoped<DaysService>();
builder.Services.AddScoped<IEventModule, NeuroModule>();
builder.Services.AddScoped<NeuroService>();
builder.Services.AddSingleton(_ => NeuroCatalog.Load(Path.Combine(AppContext.BaseDirectory, "Neuro", "neuro-rounds.json")));
builder.Services.AddScoped<QuizContentService>();
builder.Services.AddScoped<IEventModule, QuizModule>();
builder.Services.AddScoped<QuizService>();
builder.Services.AddSingleton<TvPreloadStatus>();
builder.Services.AddScoped<AuctionLotService>();
builder.Services.AddScoped<IEventModule, AuctionModule>();
builder.Services.AddScoped<AuctionService>();
builder.Services.AddScoped<IEventModule, MafiaModule>();
builder.Services.AddScoped<MafiaService>();
builder.Services.AddScoped<PetService>();
builder.Services.AddScoped<IEventModule, BossModule>();
builder.Services.AddScoped<BossService>();
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
    await scope.ServiceProvider.GetRequiredService<TvContentService>().SeedCaptionsAsync();
}

app.UseForwardedHeaders();

// Файлы (видео Своей игры) — до 100 МБ; остальные запросы — со стандартным лимитом сервера (~30 МБ).
app.Use((context, next) =>
{
    if (context.Request.HasFormContentType && context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
    {
        limit.MaxRequestBodySize = AvatarEndpoints.MaxUploadBytes + 1024 * 1024;
    }

    return next(context);
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();
app.MapLeaderboard();
app.MapAdmin();
app.MapRegistration();
app.MapMedia();
app.MapTv();
app.MapEvents();
app.MapTournament();
app.MapTote();
app.MapLaugh();
app.MapHideSeek();
app.MapPhotoHunt();
app.MapCeremony();
app.MapReaction();
app.MapTapper();
app.MapDays();
app.MapNeuro();
app.MapQuizContent();
app.MapQuiz();
app.MapQuizPreload();
app.MapAuctionLots();
app.MapAuction();
app.MapMafia();
app.MapPets();
app.MapBoss();
app.MapHub<LiveHub>("/hub");
app.MapSpa();

app.Run();

/// <summary>
/// Точка входа; partial — для тестового хоста.
/// </summary>
public partial class Program;
