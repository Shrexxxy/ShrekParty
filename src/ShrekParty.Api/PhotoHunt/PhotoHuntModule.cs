using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.PhotoHunt;

/// <summary>Фаза фотоохоты.</summary>
public enum PhotoPhase
{
    /// <summary>Снимают — до дедлайна.</summary>
    Shooting,

    /// <summary>Время вышло: стена фото видна всем, игроки лайкают.</summary>
    Rating,
}

/// <summary>Настройки запуска: задание и сколько секунд на него.</summary>
public record PhotoHuntSettings(string? Task = null, int? DurationSec = null);

/// <summary>Состояние в Event.StateJson.</summary>
public record PhotoHuntState(PhotoPhase Phase, DateTimeOffset Deadline, string Task);

/// <summary>Фото на стене с лайками.</summary>
public record HuntPhotoDto(Guid UserId, string Login, string? AvatarUrl, string PhotoUrl, int Likes, bool LikedByMe);

/// <summary>Детали фотоохоты.</summary>
/// <param name="Photos">Админу — всегда, остальным — после дедлайна (стена).</param>
public record PhotoHuntDetails(string Task, PhotoPhase Phase, DateTimeOffset Deadline, string? MyPhotoUrl, int Submitted, int Total, IReadOnlyList<HuntPhotoDto> Photos);

/// <summary>
/// Один запуск фотоохоты: задание + таймер, фото можно переснимать до дедлайна; потом лайки, места по лайкам.
/// </summary>
public class PhotoHuntModule(AppDbContext db, TimeProvider time) : IEventModule
{
    /// <summary>Сколько даётся на задание по умолчанию.</summary>
    public const int DefaultDurationSec = 180;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.PhotoHunt;

    /// <inheritdoc/>
    public Task StartAsync(Event ev)
    {
        var settings = ev.SettingsJson is null ? null : JsonSerializer.Deserialize<PhotoHuntSettings>(ev.SettingsJson, Json);
        var task = string.IsNullOrWhiteSpace(settings?.Task) ? ev.Title : settings.Task.Trim();
        var duration = TimeSpan.FromSeconds(settings?.DurationSec is > 0 and var d ? d : DefaultDurationSec);
        ev.StateJson = JsonSerializer.Serialize(new PhotoHuntState(PhotoPhase.Shooting, time.GetUtcNow() + duration, task), Json);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var submissions = await db.PhotoSubmissions.AsNoTracking().Where(s => s.EventId == ev.Id).ToListAsync();
        var likes = await db.PhotoLikes.AsNoTracking().Where(l => l.EventId == ev.Id).ToListAsync();
        var users = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToDictionaryAsync(u => u.Id);
        var wallVisible = isAdmin || state.Phase == PhotoPhase.Rating;
        var photos = wallVisible
            ? submissions.Where(s => users.ContainsKey(s.UserId))
                .OrderBy(s => s.SubmittedAt)
                .Select(s => new HuntPhotoDto(s.UserId, users[s.UserId].Login, users[s.UserId].ToMe().AvatarUrl, MediaStorage.UrlFor(s.MediaId),
                    likes.Count(l => l.OwnerId == s.UserId), likes.Any(l => l.OwnerId == s.UserId && l.LikerId == viewerId)))
                .ToList()
            : [];
        var mine = submissions.FirstOrDefault(s => s.UserId == viewerId);

        return new PhotoHuntDetails(
            state.Task,
            state.Phase,
            state.Deadline,
            mine is null ? null : MediaStorage.UrlFor(mine.MediaId),
            submissions.Count,
            users.Count,
            photos);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (state.Phase == PhotoPhase.Shooting && now >= state.Deadline)
        {
            ev.StateJson = JsonSerializer.Serialize(state with { Phase = PhotoPhase.Rating }, Json);
        }

        // Завершает именинник, когда все пролайкают.
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var submitted = await db.PhotoSubmissions.Where(s => s.EventId == ev.Id).Select(s => s.UserId).ToListAsync();
        var likes = await db.PhotoLikes.Where(l => l.EventId == ev.Id)
            .GroupBy(l => l.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.OwnerId, g => g.Count);
        return DenseRanking.Rank(submitted, owner => likes.GetValueOrDefault(owner), descending: true)
            .Select(r => new Ranked<Guid>(r.Item, r.Place))
            .ToList();
    }

    internal static PhotoHuntState StateOf(Event ev) => JsonSerializer.Deserialize<PhotoHuntState>(ev.StateJson!, Json)!;
}
