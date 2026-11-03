using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;
using ShrekParty.Api.Tote;

namespace ShrekParty.Api.Tournament;

/// <summary>
/// Действия с матчами: начать, результат, отмена результата, вызов имениннику, фото.
/// Нарушение правил — <see cref="EventRuleException"/> (409), неверные данные — <see cref="BadInputException"/> (400).
/// </summary>
public class TournamentService(AppDbContext db, ILiveNotifier live, MediaStorage media, ToteService tote, TimeProvider time)
{
    /// <summary>
    /// Ставит игрока на сторону матча (null — снимает, если там стоял этот игрок).
    /// </summary>
    internal static void Put(IReadOnlyList<TournamentMatch> matches, int? index, MatchSlot? slot, Guid? player)
    {
        if (index is null || player is null)
        {
            return;
        }

        var target = matches.Single(m => m.Index == index);
        if (slot == MatchSlot.A)
        {
            target.PlayerAId = player;
        }
        else
        {
            target.PlayerBId = player;
        }
    }

    /// <summary>
    /// Матч начался — ставки закрыты.
    /// </summary>
    public async Task StartMatchAsync(Guid matchId)
    {
        var (match, _, _) = await LoadAsync(matchId);
        if (match.Status != MatchStatus.Pending || match.PlayerAId is null || match.PlayerBId is null)
        {
            throw new EventRuleException("not_ready");
        }

        match.Status = MatchStatus.Live;
        match.StartedAt = time.GetUtcNow();
        await SaveAsync();
    }

    /// <summary>
    /// Результат матча: победитель проходит дальше, проигравший полуфинала — в матч за 3-е.
    /// </summary>
    public async Task SetWinnerAsync(Guid matchId, Guid winnerId, string? score)
    {
        var (match, matches, _) = await LoadAsync(matchId);
        if (match.Status == MatchStatus.Done || match.PlayerAId is null || match.PlayerBId is null)
        {
            throw new EventRuleException("not_ready");
        }
        if (winnerId != match.PlayerAId && winnerId != match.PlayerBId)
        {
            throw new BadInputException("winner_not_in_match");
        }

        match.Status = MatchStatus.Done;
        match.WinnerId = winnerId;
        match.Score = string.IsNullOrWhiteSpace(score) ? null : score.Trim();
        match.StartedAt ??= time.GetUtcNow();
        match.FinishedAt = time.GetUtcNow();
        Put(matches, match.NextIndex, match.NextSlot, winnerId);
        Put(matches, match.LoserNextIndex, match.LoserNextSlot, match.LoserId);
        await tote.SettleAsync(match);
        await SaveAsync();
    }

    /// <summary>
    /// Отмена результата (мисклик): можно, пока следующие матчи не начались.
    /// </summary>
    public async Task UndoAsync(Guid matchId)
    {
        var (match, matches, ev) = await LoadAsync(matchId);
        if (match.Status != MatchStatus.Done || match.IsBye)
        {
            throw new EventRuleException("not_done");
        }

        var next = matches.SingleOrDefault(m => m.Index == match.NextIndex);
        var loserNext = matches.SingleOrDefault(m => m.Index == match.LoserNextIndex);
        var challengeStarted = match.Round == TournamentRound.Final && TournamentModule.StateOf(ev).Decision is not null;
        if (next?.Status is MatchStatus.Live or MatchStatus.Done
            || loserNext?.Status is MatchStatus.Live or MatchStatus.Done
            || challengeStarted)
        {
            throw new EventRuleException("next_started");
        }

        Clear(next, match.NextSlot);
        Clear(loserNext, match.LoserNextSlot);
        await tote.UnsettleAsync(match);
        match.WinnerId = null;
        match.Score = null;
        match.FinishedAt = null;
        match.Status = MatchStatus.Live;
        await SaveAsync();
    }

    /// <summary>
    /// Решение победителя финала: принять вызов (матч с именинником) или забрать 100%.
    /// </summary>
    public async Task DecideChallengeAsync(Guid eventId, Guid playerId, bool accept)
    {
        var ev = await db.ActiveOfTypeAsync(eventId, EventType.Tournament);
        var matches = await db.TournamentMatches.Where(m => m.EventId == eventId).ToListAsync();
        var challenge = TournamentModule.ChallengeOf(ev, matches);
        if (challenge.State == ChallengeState.None || challenge.OfferedToId != playerId)
        {
            throw new UnauthorizedAccessException();
        }
        if (challenge.State != ChallengeState.Offered)
        {
            throw new EventRuleException("already_decided");
        }

        ev.StateJson = JsonSerializer.Serialize(
            new TournamentState(accept ? ChallengeState.Accepted : ChallengeState.Declined), TournamentModule.Json);
        if (accept)
        {
            var admin = await db.Users.FirstAsync(u => u.Role == UserRole.Admin);
            db.TournamentMatches.Add(new TournamentMatch
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Index = matches.Max(m => m.Index) + 1,
                Round = TournamentRound.Challenge,
                PlayerAId = playerId,
                PlayerBId = admin.Id,
                IsAdminB = true,
                Status = MatchStatus.Pending,
            });
        }

        await SaveAsync();
    }

    /// <summary>
    /// Фото матча — для карусели ТВ с подписью. onlyLive — снимает гость: только пока матч идёт (ведущий — когда угодно).
    /// </summary>
    public async Task AddPhotoAsync(Guid matchId, Stream content, string contentType, Guid authorId, bool onlyLive = false)
    {
        var (match, _, _) = await LoadAsync(matchId);
        if (onlyLive && match.Status != MatchStatus.Live)
        {
            throw new EventRuleException("not_live");
        }

        await media.SaveAsync(content, contentType, MediaKind.Photo, authorId, eventId: match.EventId, matchId: match.Id, tag: "match");
        await SaveAsync();
    }

    private static void Clear(TournamentMatch? target, MatchSlot? slot)
    {
        if (target is null)
        {
            return;
        }

        if (slot == MatchSlot.A)
        {
            target.PlayerAId = null;
        }
        else
        {
            target.PlayerBId = null;
        }
    }

    private async Task<(TournamentMatch Match, List<TournamentMatch> Matches, Event Event)> LoadAsync(Guid matchId)
    {
        var match = await db.TournamentMatches.FirstOrDefaultAsync(m => m.Id == matchId) ?? throw new KeyNotFoundException();
        var ev = await db.Events.FirstAsync(e => e.Id == match.EventId);
        if (ev.Status != EventStatus.Active)
        {
            throw new EventRuleException("not_active");
        }

        var matches = await db.TournamentMatches.Where(m => m.EventId == match.EventId).ToListAsync();
        return (matches.Single(m => m.Id == matchId), matches, ev);
    }

    private async Task SaveAsync()
    {
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }
}
