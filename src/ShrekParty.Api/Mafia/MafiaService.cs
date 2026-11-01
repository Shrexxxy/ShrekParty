using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Mafia;

/// <summary>Ночной ход: на кого.</summary>
public record MafiaNightRequest(Guid TargetId);

/// <summary>Голос: за кого; null — воздержался.</summary>
public record MafiaVoteRequest(Guid? TargetId);

/// <summary>
/// Действия в мафии: ночные ходы, голос, «закончил речь» (игроки) и «Дальше» (ведущий).
/// </summary>
public class MafiaService(AppDbContext db, ILiveNotifier live, TimeProvider time)
{
    /// <summary>
    /// Ночной ход по роли: мафия — жертва, доктор — кого лечит, комиссар — кого проверяет, мирный — заглушка без последствий.
    /// </summary>
    public Task NightAsync(Guid eventId, Guid userId, Guid targetId) => ChangeAsync(eventId, state =>
    {
        if (state.Phase != MafiaPhase.Night)
        {
            throw new EventRuleException("not_night");
        }

        var me = AliveSeat(state, userId);
        var target = state.Seats.FirstOrDefault(s => s.UserId == targetId && s.Alive) ?? throw new BadInputException("bad_target");
        var checks = state.Checks;
        switch (me.Role)
        {
            case MafiaRole.Mafia when target.Role == MafiaRole.Mafia:
                throw new BadInputException("bad_target");
            case MafiaRole.Doctor when target.UserId == userId || target.UserId == state.LastHeal:
                throw new BadInputException("bad_target");
            case MafiaRole.Commissar:
                if (state.NightActions.ContainsKey(userId))
                {
                    throw new EventRuleException("already");
                }
                if (target.UserId == userId)
                {
                    throw new BadInputException("bad_target");
                }

                checks = [.. checks, new MafiaCheck(state.Day, target.UserId, target.Role == MafiaRole.Mafia)];
                break;
        }

        var next = state with
        {
            NightActions = new Dictionary<Guid, Guid>(state.NightActions) { [userId] = targetId },
            Checks = checks,
        };
        return MafiaModule.NightComplete(next) ? MafiaModule.Advance(next, time.GetUtcNow()) : next;
    });

    /// <summary>
    /// Голос: один раз, не за себя; все живые проголосовали — сразу итог.
    /// </summary>
    public Task VoteAsync(Guid eventId, Guid userId, Guid? targetId) => ChangeAsync(eventId, state =>
    {
        if (state.Phase != MafiaPhase.Voting)
        {
            throw new EventRuleException("not_voting");
        }

        AliveSeat(state, userId);
        if (state.Votes.ContainsKey(userId))
        {
            throw new EventRuleException("already");
        }
        if (targetId is { } t && (t == userId || !state.Seats.Any(s => s.UserId == t && s.Alive)))
        {
            throw new BadInputException("bad_target");
        }

        var next = state with { Votes = new Dictionary<Guid, Guid?>(state.Votes) { [userId] = targetId } };
        return state.Seats.Where(s => s.Alive).All(s => next.Votes.ContainsKey(s.UserId))
            ? MafiaModule.Advance(next, time.GetUtcNow())
            : next;
    });

    /// <summary>
    /// Говорящий закончил раньше — слово следующему.
    /// </summary>
    public Task SpeechDoneAsync(Guid eventId, Guid userId) => ChangeAsync(eventId, state =>
        state.Phase == MafiaPhase.Speech && state.SpeechQueue.ElementAtOrDefault(state.SpeakerIndex) == userId
            ? MafiaModule.Advance(state, time.GetUtcNow())
            : throw new UnauthorizedAccessException());

    /// <summary>
    /// Ведущий: закончить текущую фазу раньше (в речах — «Передать слово»).
    /// </summary>
    /// <param name="step">Номер перехода, который видел ведущий; не совпал — фаза уже сменилась, второй раз не листаем.</param>
    public Task NextAsync(Guid eventId, int? step = null) => ChangeAsync(eventId, state =>
        state.Phase == MafiaPhase.Ended ? throw new EventRuleException("ended")
        : step is not null && step != state.Step ? throw new EventRuleException("phase_changed")
        : MafiaModule.Advance(state, time.GetUtcNow()));

    private static MafiaSeat AliveSeat(MafiaState state, Guid userId)
    {
        var seat = state.Seats.FirstOrDefault(s => s.UserId == userId) ?? throw new UnauthorizedAccessException();
        return seat.Alive ? seat : throw new EventRuleException("dead");
    }

    private async Task ChangeAsync(Guid eventId, Func<MafiaState, MafiaState> change)
    {
        await MafiaModule.Lock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.Mafia);
            MafiaModule.SetState(ev, change(MafiaModule.StateOf(ev)));
            await db.SaveChangesAsync();
        }
        finally
        {
            MafiaModule.Lock.Release();
        }

        await live.BroadcastAsync();
    }
}

/// <summary>
/// Маршруты мафии.
/// </summary>
public static class MafiaEndpoints
{
    /// <summary>
    /// <c>/api/mafia/{id}/night|vote|speech-done</c> (игрок), <c>/api/admin/mafia/{id}/next</c> (ведущий).
    /// </summary>
    public static IEndpointRouteBuilder MapMafia(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var player = routes.MapGroup("/api/mafia/{id:guid}").RequireAuthorization(AuthEndpoints.PlayerPolicy);

        player.MapPost("/night", async (Guid id, MafiaNightRequest body, ClaimsPrincipal user, MafiaService mafia) =>
        {
            await mafia.NightAsync(id, user.UserId()!.Value, body.TargetId);
            return Results.NoContent();
        });

        player.MapPost("/vote", async (Guid id, MafiaVoteRequest body, ClaimsPrincipal user, MafiaService mafia) =>
        {
            await mafia.VoteAsync(id, user.UserId()!.Value, body.TargetId);
            return Results.NoContent();
        });

        player.MapPost("/speech-done", async (Guid id, ClaimsPrincipal user, MafiaService mafia) =>
        {
            await mafia.SpeechDoneAsync(id, user.UserId()!.Value);
            return Results.NoContent();
        });

        routes.MapPost("/api/admin/mafia/{id:guid}/next", async (Guid id, int? step, MafiaService mafia) =>
            {
                await mafia.NextAsync(id, step);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }
}
