using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Poll;

/// <summary>Настройки голосования.</summary>
public record PollSettings(IReadOnlyList<string>? Options = null);

/// <summary>Итог голосования: победитель и была ли ничья (на ТВ — «монетка»).</summary>
public record PollResult(string Winner, bool Tie);

/// <summary>Вариант с числом голосов.</summary>
public record PollOptionDto(string Name, int Votes);

/// <summary>Детали голосования для снапшота.</summary>
public record PollDetails(IReadOnlyList<PollOptionDto> Options, string? MyVote, bool Closed, string? Winner, bool Tie);

/// <summary>
/// Исход голосования/закрытия.
/// </summary>
public enum PollOutcome
{
    Ok,
    NotFound,
    Closed,
    UnknownOption,
}

/// <summary>
/// Голосование за игру файтинг-турнира: голос можно менять до закрытия; ничья — случайный выбор среди лидеров.
/// Ослики не начисляет.
/// </summary>
public class FightPollModule(AppDbContext db, ILiveNotifier live, Random random, TimeProvider time) : IEventModule
{
    /// <summary>Варианты по умолчанию.</summary>
    public static readonly IReadOnlyList<string> DefaultOptions = ["Mortal Kombat", "UFC"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.FightPoll;

    /// <inheritdoc/>
    public Task StartAsync(Event ev) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var votes = await db.PollVotes.AsNoTracking().Where(v => v.EventId == ev.Id).ToListAsync();
        var result = ResultOf(ev);
        return new PollDetails(
            OptionsOf(ev).Select(o => new PollOptionDto(o, votes.Count(v => v.Option == o))).ToList(),
            votes.FirstOrDefault(v => v.UserId == viewerId)?.Option,
            result is not null,
            result?.Winner,
            result?.Tie ?? false);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now) => Task.FromResult(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        // Забыли закрыть — закрываем при завершении, чтобы итог остался для турнира.
        if (ResultOf(ev) is null)
        {
            await DecideAsync(ev);
        }

        return [];
    }

    /// <summary>
    /// Голос игрока (повторный голос заменяет предыдущий).
    /// </summary>
    public async Task<PollOutcome> VoteAsync(Guid eventId, Guid userId, string? option)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.Type == EventType.FightPoll && e.Status == EventStatus.Active);
        if (ev is null)
        {
            return PollOutcome.NotFound;
        }
        if (ResultOf(ev) is not null)
        {
            return PollOutcome.Closed;
        }
        if (option is null || !OptionsOf(ev).Contains(option))
        {
            return PollOutcome.UnknownOption;
        }

        var vote = await db.PollVotes.FirstOrDefaultAsync(v => v.EventId == eventId && v.UserId == userId);
        if (vote is null)
        {
            db.PollVotes.Add(new PollVote { Id = Guid.NewGuid(), EventId = eventId, UserId = userId, Option = option, VotedAt = time.GetUtcNow() });
        }
        else
        {
            vote.Option = option;
            vote.VotedAt = time.GetUtcNow();
        }

        await db.SaveChangesAsync();
        await live.BroadcastAsync();
        return PollOutcome.Ok;
    }

    /// <summary>
    /// Закрывает голосование и фиксирует победителя; ивент остаётся на экранах до «Завершить».
    /// </summary>
    public async Task<PollOutcome> CloseAsync(Guid eventId)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.Type == EventType.FightPoll && e.Status == EventStatus.Active);
        if (ev is null)
        {
            return PollOutcome.NotFound;
        }
        if (ResultOf(ev) is not null)
        {
            return PollOutcome.Closed;
        }

        await DecideAsync(ev);
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
        return PollOutcome.Ok;
    }

    private async Task DecideAsync(Event ev)
    {
        var options = OptionsOf(ev);
        var votes = await db.PollVotes.Where(v => v.EventId == ev.Id).ToListAsync();
        var counts = options.Select(o => (Option: o, Votes: votes.Count(v => v.Option == o))).ToList();
        var best = counts.Max(c => c.Votes);
        var leaders = counts.Where(c => c.Votes == best).Select(c => c.Option).ToList();
        var result = new PollResult(leaders[random.Next(leaders.Count)], leaders.Count > 1);
        ev.ResultJson = JsonSerializer.Serialize(result, Json);
    }

    private static IReadOnlyList<string> OptionsOf(Event ev)
    {
        var settings = ev.SettingsJson is null ? null : JsonSerializer.Deserialize<PollSettings>(ev.SettingsJson, Json);
        return settings?.Options is { Count: >= 2 } options ? options : DefaultOptions;
    }

    private static PollResult? ResultOf(Event ev) =>
        ev.ResultJson is null ? null : JsonSerializer.Deserialize<PollResult>(ev.ResultJson, Json);
}
