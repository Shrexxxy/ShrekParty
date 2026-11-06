using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Neuro;

/// <summary>Фаза одного раунда.</summary>
public enum NeuroPhase
{
    /// <summary>Угадывают, 20 секунд.</summary>
    Guessing,

    /// <summary>Кто на фото и под кого, кто угадал, 12 секунд.</summary>
    Reveal,
}

/// <summary>Состояние в Event.StateJson; порядок раундов перемешивается на старте.</summary>
public record NeuroState(int Index, NeuroPhase Phase, DateTimeOffset Deadline, IReadOnlyList<string> RoundIds);

/// <summary>Детали нейрофото.</summary>
/// <param name="Index">Номер текущего раунда с нуля.</param>
/// <param name="PhotoUrl">Что получилось — маскировка.</param>
/// <param name="People">Люди на выбор.</param>
/// <param name="Characters">Персонажи на выбор.</param>
/// <param name="Guessed">Сколько уже ответили в этом раунде.</param>
/// <param name="Answer">Кто на фото — с раскрытия.</param>
/// <param name="Character">Под кого замаскирован — с раскрытия.</param>
/// <param name="MyPoints">Сколько осликов за раунд получил зритель — с раскрытия.</param>
/// <param name="Correct">Логины угадавших человека — с раскрытия.</param>
/// <param name="FriendUrl">Фото друга — с раскрытия.</param>
/// <param name="SourceUrl">Картинка-образ — с раскрытия.</param>
public record NeuroDetails(
    int Index,
    int Total,
    NeuroPhase Phase,
    DateTimeOffset Deadline,
    string PhotoUrl,
    IReadOnlyList<string> People,
    IReadOnlyList<string> Characters,
    string? MyPerson,
    string? MyCharacter,
    int Guessed,
    string? Answer,
    string? Character,
    int? MyPoints,
    IReadOnlyList<string> Correct,
    string? FriendUrl,
    string? SourceUrl);

/// <summary>
/// Угадай по нейрофото: друг замаскирован под персонажа. Раунд — 20 секунд угадать человека и персонажа,
/// 12 секунд раскрытие (на ТВ три фото: друг, образ, результат). Ослики — сразу на раскрытии:
/// человек +2, персонаж +2, оба +5. Мест и линейной схемы нет.
/// </summary>
public class NeuroModule(AppDbContext db, NeuroCatalog catalog, Random random, TimeProvider time) : IEventModule
{
    /// <summary>Время на угадывание одного фото.</summary>
    public static readonly TimeSpan GuessTime = TimeSpan.FromSeconds(40);

    /// <summary>Сколько показываем ответ — успеть рассмотреть три фото.</summary>
    public static readonly TimeSpan RevealTime = TimeSpan.FromSeconds(12);

    /// <summary>За угаданного человека или только персонажа.</summary>
    public const int OnePartPoints = 2;

    /// <summary>За человека и персонажа вместе — больше, чем 2 + 2.</summary>
    public const int BothPoints = 5;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.NeuroPhoto;

    /// <summary>Ослики за раунд.</summary>
    public static int PointsFor(NeuroGuess guess, NeuroRound round)
    {
        var person = guess.Person == round.Answer;
        var character = guess.Character == round.Character;
        return person && character ? BothPoints : person || character ? OnePartPoints : 0;
    }

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        await db.EnsurePlayersAsync();
        if (catalog.Rounds.Count == 0)
        {
            throw new EventRuleException("neuro_not_ready");
        }

        var ids = catalog.Rounds.Select(r => r.Id).ToArray();
        random.Shuffle(ids);
        SetState(ev, new NeuroState(0, NeuroPhase.Guessing, time.GetUtcNow() + GuessTime, ids));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var round = RoundOf(state);
        var guesses = await db.NeuroGuesses.AsNoTracking().Where(g => g.EventId == ev.Id && g.RoundId == round.Id).ToListAsync();
        var mine = guesses.FirstOrDefault(g => g.UserId == viewerId);
        var reveal = state.Phase == NeuroPhase.Reveal;
        var correct = reveal ? await LoginsAsync(guesses.Where(g => g.Person == round.Answer).Select(g => g.UserId).ToList()) : [];

        return new NeuroDetails(
            state.Index,
            state.RoundIds.Count,
            state.Phase,
            state.Deadline,
            round.ResultUrl,
            NeuroCatalog.Names,
            catalog.Characters,
            mine?.Person,
            mine?.Character,
            guesses.Count,
            reveal ? round.Answer : null,
            reveal ? round.Character : null,
            reveal && viewerId is not null ? (mine is null ? 0 : PointsFor(mine, round)) : null,
            correct,
            reveal ? round.FriendUrl : null,
            reveal ? round.SourceUrl : null);
    }

    /// <inheritdoc/>
    public async Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (now < state.Deadline)
        {
            return false;
        }

        if (state.Phase == NeuroPhase.Guessing)
        {
            await AwardAsync(ev, RoundOf(state), now);
            SetState(ev, state with { Phase = NeuroPhase.Reveal, Deadline = now + RevealTime });
            return false;
        }

        if (state.Index + 1 >= state.RoundIds.Count)
        {
            return true;
        }

        SetState(ev, state with { Index = state.Index + 1, Phase = NeuroPhase.Guessing, Deadline = now + GuessTime });
        return false;
    }

    /// <inheritdoc/>
    /// <remarks>Ослики уже начислены на раскрытиях — при завершении добавлять нечего.</remarks>
    public Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev) => Task.FromResult<IReadOnlyList<Ranked<Guid>>>([]);

    internal static NeuroState StateOf(Event ev) => JsonSerializer.Deserialize<NeuroState>(ev.StateJson!, Json)!;

    private NeuroRound RoundOf(NeuroState state) => catalog.Rounds.Single(r => r.Id == state.RoundIds[state.Index]);

    /// <summary>Ослики за раунд — сразу, чтобы таблица росла по ходу игры.</summary>
    private async Task AwardAsync(Event ev, NeuroRound round, DateTimeOffset now)
    {
        var guesses = await db.NeuroGuesses.AsNoTracking().Where(g => g.EventId == ev.Id && g.RoundId == round.Id).ToListAsync();
        foreach (var guess in guesses)
        {
            var points = PointsFor(guess, round);
            if (points > 0)
            {
                db.DonkeyAwards.Add(new DonkeyAward
                {
                    Id = Guid.NewGuid(), UserId = guess.UserId, EventId = ev.Id, Amount = points, Reason = $"{ev.Title}: {round.Answer}", CreatedAt = now,
                });
            }
        }
    }

    private async Task<IReadOnlyList<string>> LoginsAsync(IReadOnlyList<Guid> userIds) =>
        (await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => u.Login).ToListAsync())
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static void SetState(Event ev, NeuroState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);
}
