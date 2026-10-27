using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;
using ShrekParty.Api.Scoring;
using ShrekParty.Api.Tournament;

namespace ShrekParty.Api.Neuro;

/// <summary>Фаза одного фото.</summary>
public enum NeuroPhase
{
    /// <summary>Угадывают, 20 секунд.</summary>
    Guessing,

    /// <summary>Кто на фото и кто угадал, 5 секунд.</summary>
    Reveal,
}

/// <summary>Состояние в Event.StateJson; порядок фото фиксируется на старте.</summary>
public record NeuroState(int Index, NeuroPhase Phase, DateTimeOffset Deadline, IReadOnlyList<Guid> PhotoIds);

/// <summary>Вариант ответа: игрок или именинник.</summary>
public record NeuroOptionDto(Guid UserId, string Login, string? AvatarUrl);

/// <summary>Детали нейрофото.</summary>
/// <param name="Index">Номер текущего фото с нуля.</param>
/// <param name="Guessed">Сколько уже ответили на это фото.</param>
/// <param name="SubjectId">Кто на фото — с раскрытия.</param>
/// <param name="Correct">Логины угадавших — с раскрытия.</param>
public record NeuroDetails(
    int Index,
    int Total,
    NeuroPhase Phase,
    DateTimeOffset Deadline,
    string PhotoUrl,
    IReadOnlyList<NeuroOptionDto> Options,
    Guid? MyGuess,
    int Guessed,
    Guid? SubjectId,
    IReadOnlyList<string> Correct);

/// <summary>
/// Угадай по нейрофото: каждое фото — 20 секунд угадывать и 5 секунд раскрытие; место по числу угаданных.
/// </summary>
public class NeuroModule(AppDbContext db, TimeProvider time) : IEventModule
{
    /// <summary>Время на угадывание одного фото.</summary>
    public static readonly TimeSpan GuessTime = TimeSpan.FromSeconds(20);

    /// <summary>Сколько показываем ответ.</summary>
    public static readonly TimeSpan RevealTime = TimeSpan.FromSeconds(5);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.NeuroPhoto;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        await db.EnsurePlayersAsync();

        var photos = (await db.NeuroPhotos.ToListAsync()).OrderBy(p => p.CreatedAt).ToList();
        if (photos.Count == 0 || photos.Any(p => p.SubjectId is null))
        {
            throw new EventRuleException("neuro_not_ready");
        }

        SetState(ev, new NeuroState(0, NeuroPhase.Guessing, time.GetUtcNow() + GuessTime, photos.Select(p => p.Id).ToList()));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var photoId = state.PhotoIds[state.Index];
        var photo = await db.NeuroPhotos.AsNoTracking().FirstAsync(p => p.Id == photoId);
        var guesses = await db.NeuroGuesses.AsNoTracking().Where(g => g.EventId == ev.Id && g.PhotoId == photoId).ToListAsync();
        var options = await OptionsAsync(db);
        var reveal = state.Phase == NeuroPhase.Reveal;
        var logins = options.ToDictionary(o => o.UserId, o => o.Login);

        return new NeuroDetails(
            state.Index,
            state.PhotoIds.Count,
            state.Phase,
            state.Deadline,
            MediaStorage.UrlFor(photo.MediaId),
            options,
            guesses.FirstOrDefault(g => g.UserId == viewerId)?.GuessId,
            guesses.Count,
            reveal ? photo.SubjectId : null,
            reveal
                ? guesses.Where(g => g.GuessId == photo.SubjectId && logins.ContainsKey(g.UserId)).Select(g => logins[g.UserId]).Order(StringComparer.OrdinalIgnoreCase).ToList()
                : []);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (now < state.Deadline)
        {
            return Task.FromResult(false);
        }

        if (state.Phase == NeuroPhase.Guessing)
        {
            SetState(ev, state with { Phase = NeuroPhase.Reveal, Deadline = now + RevealTime });
            return Task.FromResult(false);
        }

        if (state.Index + 1 >= state.PhotoIds.Count)
        {
            return Task.FromResult(true);
        }

        SetState(ev, state with { Index = state.Index + 1, Phase = NeuroPhase.Guessing, Deadline = now + GuessTime });
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var subjects = await db.NeuroPhotos.Where(p => p.SubjectId != null).ToDictionaryAsync(p => p.Id, p => p.SubjectId!.Value);
        var guesses = await db.NeuroGuesses.Where(g => g.EventId == ev.Id).ToListAsync();
        var correct = guesses
            .Where(g => subjects.TryGetValue(g.PhotoId, out var subject) && subject == g.GuessId)
            .GroupBy(g => g.UserId)
            .Select(g => (UserId: g.Key, Count: g.Count()))
            .ToList();
        return DenseRanking.Rank(correct, c => c.Count, descending: true)
            .Select(r => new Ranked<Guid>(r.Item.UserId, r.Place))
            .ToList();
    }

    /// <summary>Кого можно выбрать: все игроки и именинник.</summary>
    internal static async Task<IReadOnlyList<NeuroOptionDto>> OptionsAsync(AppDbContext db) =>
        (await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player || u.Role == UserRole.Admin).ToListAsync())
        .Select(u => new NeuroOptionDto(u.Id, u.Role == UserRole.Admin ? TournamentModule.AdminDisplayName : u.Login, u.ToMe().AvatarUrl))
        .OrderBy(o => o.Login, StringComparer.OrdinalIgnoreCase)
        .ToList();

    internal static NeuroState StateOf(Event ev) => JsonSerializer.Deserialize<NeuroState>(ev.StateJson!, Json)!;

    private static void SetState(Event ev, NeuroState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);
}
