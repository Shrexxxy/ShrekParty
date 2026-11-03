using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Scoring;
using ShrekParty.Api.Tote;

namespace ShrekParty.Api.Tournament;

/// <summary>
/// Турнир на PS5: сетка из всех игроков, места по сетке, поправка за «вызов имениннику».
/// Действия с матчами — в <see cref="TournamentService"/>.
/// </summary>
public class TournamentModule(AppDbContext db, Random random) : IEventModule
{
    /// <summary>Подпись именинника в сетке.</summary>
    public const string AdminDisplayName = "Именинник";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Tournament;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        var players = await db.Users.Where(u => u.Role == UserRole.Player).Select(u => u.Id).ToListAsync();
        if (players.Count < 2)
        {
            throw new EventRuleException("not_enough_players");
        }

        foreach (var planned in BracketBuilder.Build(players, random))
        {
            db.TournamentMatches.Add(new TournamentMatch
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                Index = planned.Index,
                Round = planned.Round,
                RoundIndex = planned.RoundIndex,
                PlayerAId = planned.PlayerA,
                PlayerBId = planned.PlayerB,
                IsBye = planned.IsBye,
                NextIndex = planned.NextIndex,
                NextSlot = planned.NextSlot,
                LoserNextIndex = planned.LoserNextIndex,
                LoserNextSlot = planned.LoserNextSlot,
                Status = MatchStatus.Pending,
            });
        }

        await db.SaveChangesAsync();

        // Проходы без игры — сразу дальше.
        var matches = await db.TournamentMatches.Where(m => m.EventId == ev.Id).ToListAsync();
        foreach (var bye in matches.Where(m => m.IsBye))
        {
            bye.Status = MatchStatus.Done;
            bye.WinnerId = bye.PlayerAId;
            TournamentService.Put(matches, bye.NextIndex, bye.NextSlot, bye.WinnerId);
        }

        ev.StateJson = JsonSerializer.Serialize(new TournamentState(), Json);
        db.Events.Add(new Event
        {
            Id = Guid.NewGuid(),
            Type = EventType.Tote,
            Title = $"Тотализатор: {ev.Title}",
            Status = EventStatus.Active,
            ParentId = ev.Id,
            StartedAt = ev.StartedAt,
        });
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var matches = (await db.TournamentMatches.AsNoTracking().Where(m => m.EventId == ev.Id).ToListAsync())
            .OrderBy(m => m.Index)
            .ToList();
        var ids = matches.SelectMany(m => new[] { m.PlayerAId, m.PlayerBId }).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var photos = await db.MediaFiles.AsNoTracking()
            .Where(f => f.MatchId != null && matches.Select(m => m.Id).Contains(f.MatchId.Value))
            .GroupBy(f => f.MatchId)
            .Select(g => new { MatchId = g.Key!.Value, Count = g.Count() })
            .ToDictionaryAsync(g => g.MatchId, g => g.Count);

        var matchIds = matches.Select(m => m.Id).ToList();
        var bets = (await db.Bets.AsNoTracking().Where(b => matchIds.Contains(b.MatchId)).ToListAsync())
            .GroupBy(b => b.MatchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        TournamentPlayerDto? Player(Guid? id)
        {
            if (id is null || !users.TryGetValue(id.Value, out var user))
            {
                return null;
            }

            var isAdminUser = user.Role == UserRole.Admin;
            return new TournamentPlayerDto(user.Id, isAdminUser ? AdminDisplayName : user.Login, user.ToMe().AvatarUrl, isAdminUser);
        }

        var dtos = matches.Select(m =>
            {
                var matchBets = bets.GetValueOrDefault(m.Id) ?? [];
                var forOdds = matchBets.Select(b => new PariMutuelBet(b.Id, b.OnPlayerId, b.Amount)).ToList();
                var mine = matchBets.FirstOrDefault(b => b.UserId == viewerId);
                return new MatchDto(
                    m.Id, m.Index, m.Round, m.RoundIndex, Player(m.PlayerAId), Player(m.PlayerBId), m.IsBye, m.Status,
                    m.WinnerId, m.Score, photos.GetValueOrDefault(m.Id),
                    matchBets.Sum(b => b.Amount),
                    m.PlayerAId is { } a ? PariMutuel.Odds(forOdds, a) : null,
                    m.PlayerBId is { } b ? PariMutuel.Odds(forOdds, b) : null,
                    mine is null ? null : new MyBetDto(mine.OnPlayerId, mine.Amount),
                    m.NextIndex);
            })
            .ToList();

        var next = viewerId is null
            ? null
            : matches.FirstOrDefault(m => m.Status != MatchStatus.Done && (m.PlayerAId == viewerId || m.PlayerBId == viewerId))?.Id;

        var challenge = ChallengeOf(ev, matches);
        return new TournamentDetails(
            ev.Title, dtos, next, challenge with { OfferedToMe = viewerId is not null && challenge.OfferedToId == viewerId }, ToteService.CurrentBetMatch(matches)?.Id);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now) => Task.FromResult(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var matches = await db.TournamentMatches.Where(m => m.EventId == ev.Id).ToListAsync();
        var decision = StateOf(ev).Decision;
        var bracketDone = matches.Where(m => m.Round != TournamentRound.Challenge).All(m => m.Status == MatchStatus.Done);
        // Победитель ещё думает про вызов — ведущий может завершить: молчание засчитывается как отказ (см. AfterFinishAsync).
        var challengeDone = decision != ChallengeState.Accepted
            || matches.Any(m => m.Round == TournamentRound.Challenge && m.Status == MatchStatus.Done);
        if (!bracketDone || !challengeDone)
        {
            throw new EventRuleException("not_complete");
        }

        return TournamentRanking.Rank(matches
            .Where(m => m.Round != TournamentRound.Challenge)
            .Select(m => new MatchResult(m.Round, m.WinnerId, m.LoserId)));
    }

    /// <inheritdoc/>
    public async Task AfterFinishAsync(Event ev)
    {
        var tote = await db.Events.FirstOrDefaultAsync(e => e.ParentId == ev.Id && e.Type == EventType.Tote);
        if (tote is not null)
        {
            tote.Status = EventStatus.Finished;
            tote.FinishedAt = ev.FinishedAt;
        }

        if (StateOf(ev).Decision is null)
        {
            ev.StateJson = JsonSerializer.Serialize(new TournamentState(ChallengeState.Declined), Json);
        }

        var challenge = await db.TournamentMatches.FirstOrDefaultAsync(m => m.EventId == ev.Id && m.Round == TournamentRound.Challenge);
        if (challenge?.WinnerId is null || challenge.PlayerAId is not { } champion)
        {
            return;
        }

        // Ослики за 1-е место уже начислены — добавляем поправку отдельной строкой.
        var firstPlace = await db.DonkeyAwards.Where(a => a.EventId == ev.Id && a.UserId == champion).SumAsync(a => a.Amount);
        var won = challenge.WinnerId == champion;
        var target = won ? firstPlace * 2 : (int)Math.Round(firstPlace * 0.5, MidpointRounding.AwayFromZero);
        db.DonkeyAwards.Add(new DonkeyAward
        {
            Id = Guid.NewGuid(),
            UserId = champion,
            EventId = ev.Id,
            Amount = target - firstPlace,
            Reason = won ? "Вызов имениннику: победа ×2" : "Вызов имениннику: поражение ×0,5",
            CreatedAt = ev.FinishedAt ?? DateTimeOffset.UtcNow,
        });
    }

    internal static TournamentState StateOf(Event ev) =>
        ev.StateJson is null ? new TournamentState() : JsonSerializer.Deserialize<TournamentState>(ev.StateJson, Json) ?? new TournamentState();

    internal static ChallengeDto ChallengeOf(Event ev, IReadOnlyList<TournamentMatch> matches)
    {
        var final = matches.Single(m => m.Round == TournamentRound.Final);
        if (final.Status != MatchStatus.Done)
        {
            return new ChallengeDto(ChallengeState.None, null, false);
        }

        return new ChallengeDto(StateOf(ev).Decision ?? ChallengeState.Offered, final.WinnerId, false);
    }
}
