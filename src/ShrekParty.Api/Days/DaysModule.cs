using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Days;

/// <summary>Фаза «Сколько мне дней».</summary>
public enum DaysPhase
{
    /// <summary>Отвечают, 30 секунд.</summary>
    Answering,

    /// <summary>Раскрытие ответов и правды на ТВ; завершает именинник.</summary>
    Reveal,
}

/// <summary>Состояние в Event.StateJson.</summary>
public record DaysState(DaysPhase Phase, DateTimeOffset Deadline);

/// <summary>Ответ при раскрытии.</summary>
public record DaysAnswerDto(string Login, string? AvatarUrl, int Days, int Diff);

/// <summary>Детали «Сколько мне дней».</summary>
/// <param name="Truth">Правда — админу всегда, остальным с раскрытия.</param>
/// <param name="Disqualified">Логины «недобросовестных» — видно всем сразу.</param>
/// <param name="Answers">С раскрытия: от самого далёкого ответа к самому близкому.</param>
public record DaysDetails(
    DaysPhase Phase,
    DateTimeOffset Deadline,
    int? MyAnswer,
    bool IAmDisqualified,
    int Answered,
    int Total,
    IReadOnlyList<string> Disqualified,
    int? Truth,
    IReadOnlyList<DaysAnswerDto> Answers);

/// <summary>
/// Сколько мне дней: 30 секунд на ответ, свернул или переключился — дисквалификация; ближе к правде — выше.
/// </summary>
public class DaysModule(AppDbContext db, IOptions<PartyOptions> party, TimeProvider time) : IEventModule
{
    /// <summary>Время на ответ.</summary>
    public static readonly TimeSpan AnswerTime = TimeSpan.FromSeconds(30);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Days;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        await db.EnsurePlayersAsync();

        SetState(ev, new DaysState(DaysPhase.Answering, time.GetUtcNow() + AnswerTime));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var truth = party.Value.AgeInDays;
        var answers = await db.DaysAnswers.AsNoTracking().Where(a => a.EventId == ev.Id).ToListAsync();
        var users = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToDictionaryAsync(u => u.Id);
        var mine = answers.FirstOrDefault(a => a.UserId == viewerId);
        var reveal = state.Phase == DaysPhase.Reveal;

        var revealed = reveal
            ? answers.Where(a => !a.Disqualified && a.Days is not null && users.ContainsKey(a.UserId))
                .Select(a => new DaysAnswerDto(users[a.UserId].Login, users[a.UserId].ToMe().AvatarUrl, a.Days!.Value, Math.Abs(a.Days.Value - truth)))
                .OrderByDescending(a => a.Diff)
                .ThenBy(a => a.Login, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        return new DaysDetails(
            state.Phase,
            state.Deadline,
            mine?.Days,
            mine?.Disqualified ?? false,
            answers.Count(a => a.Days is not null && !a.Disqualified),
            users.Count,
            answers.Where(a => a.Disqualified && users.ContainsKey(a.UserId)).Select(a => users[a.UserId].Login).ToList(),
            isAdmin || reveal ? truth : null,
            revealed);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (state.Phase == DaysPhase.Answering && now >= state.Deadline)
        {
            SetState(ev, state with { Phase = DaysPhase.Reveal });
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var truth = party.Value.AgeInDays;
        var answers = await db.DaysAnswers.Where(a => a.EventId == ev.Id && !a.Disqualified && a.Days != null).ToListAsync();
        return DenseRanking.Rank(answers, a => Math.Abs(a.Days!.Value - truth), descending: false)
            .Select(r => new Ranked<Guid>(r.Item.UserId, r.Place))
            .ToList();
    }

    internal static DaysState StateOf(Event ev) => JsonSerializer.Deserialize<DaysState>(ev.StateJson!, Json)!;

    private static void SetState(Event ev, DaysState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);
}
