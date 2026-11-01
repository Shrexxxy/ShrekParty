using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Bosses;

/// <summary>Действие в ход: что и (для лечения) кому.</summary>
public record BossActRequest(FighterAction Action, Guid? TargetId);

/// <summary>
/// Бой с боссом: выбор действия и поддержка с того света (игроки), «Дальше» (ведущий).
/// </summary>
public class BossService(AppDbContext db, ILiveNotifier live, TimeProvider time, Random random, EventEngine engine)
{
    /// <summary>
    /// Ведущий после победы: бой завершается, сразу стартует привязанный к нему аукцион с открытыми медалью лотами.
    /// </summary>
    /// <returns>Id аукциона.</returns>
    public async Task<Guid> OpenAuctionAsync(Guid bossEventId)
    {
        var boss = await db.ActiveOfTypeAsync(bossEventId, EventType.BossFight, tracking: false);
        if (BossModule.StateOf(boss).Phase != BossPhase.Won)
        {
            throw new EventRuleException("not_won");
        }

        if ((await Auction.AuctionModule.OpenPrizesAsync(db, boss)).Count == 0)
        {
            throw new EventRuleException("no_lots");
        }

        // Завершение атомарно: второе нажатие сюда не дойдёт — бой уже не идёт.
        if (await engine.FinishAsync(bossEventId) != EngineOutcome.Ok)
        {
            throw new EventRuleException("not_active");
        }

        var (_, item) = await engine.CreateAsync(new CreateEventRequest(EventType.Auction, $"{boss.Title}: аукцион"), parentId: bossEventId);
        try
        {
            if (await engine.StartAsync(item!.Id) == EngineOutcome.Ok)
            {
                return item.Id;
            }
        }
        catch (EventRuleException)
        {
            // Лоты успели уйти между проверкой и стартом — ниже уберём черновик.
        }

        await db.Events.Where(e => e.Id == item.Id).ExecuteDeleteAsync();
        throw new EventRuleException("no_lots");
    }

    /// <summary>
    /// Выбор действия на ход; можно менять до конца хода. Все живые выбрали — ход разыгрывается сразу.
    /// </summary>
    public Task ActAsync(Guid eventId, Guid userId, FighterAction action, Guid? targetId) => ChangeAsync(eventId, state =>
    {
        if (state.Phase != BossPhase.Choosing)
        {
            throw new EventRuleException("turn_over");
        }

        var me = Participant(state, userId);
        switch (BossRules.Unavailable(me, action))
        {
            case "bad_action":
                throw new BadInputException("bad_action");
            case { } reason:
                throw new EventRuleException(reason);
        }

        if (action == FighterAction.Heal && !state.Fighters.Any(f => f.UserId == targetId && f.Alive))
        {
            throw new BadInputException("bad_target");
        }

        var next = state with
        {
            Choices = new Dictionary<Guid, FighterChoice>(state.Choices)
            {
                [userId] = new FighterChoice(action, action == FighterAction.Heal ? targetId : null),
            },
        };
        return BossModule.AllChose(next) ? BossModule.Advance(next, time.GetUtcNow(), random) : next;
    });

    /// <summary>Выбывший поддерживает живых: раз за ход, прибавка к ударам — в следующем ходу.</summary>
    public Task CheerAsync(Guid eventId, Guid userId) => ChangeAsync(eventId, state =>
    {
        if (state.Phase != BossPhase.Choosing)
        {
            throw new EventRuleException("turn_over");
        }

        if (Participant(state, userId).Alive)
        {
            throw new EventRuleException("alive");
        }

        return state.Cheers.Contains(userId)
            ? throw new EventRuleException("already")
            : state with { Cheers = [.. state.Cheers, userId] };
    });

    /// <summary>
    /// Ведущий: закончить фазу раньше; шаг не совпал — таймер успел раньше, второй раз не листаем.
    /// </summary>
    public Task NextAsync(Guid eventId, int? step) => ChangeAsync(eventId, state =>
        state.Phase is BossPhase.Won or BossPhase.Lost ? throw new EventRuleException("ended")
        : step is not null && step != state.Step ? throw new EventRuleException("phase_changed")
        : BossModule.Advance(state, time.GetUtcNow(), random));

    private static Fighter Participant(BossState state, Guid userId) =>
        state.Fighters.FirstOrDefault(f => f.UserId == userId) ?? throw new UnauthorizedAccessException();

    private async Task ChangeAsync(Guid eventId, Func<BossState, BossState> change)
    {
        await BossModule.Lock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.BossFight);
            BossModule.SetState(ev, change(BossModule.StateOf(ev)));
            await db.SaveChangesAsync();
        }
        finally
        {
            BossModule.Lock.Release();
        }

        await live.BroadcastAsync();
    }
}

/// <summary>
/// Маршруты боя с боссом.
/// </summary>
public static class BossEndpoints
{
    /// <summary>
    /// <c>/api/boss/{id}/act|cheer</c> (игрок), <c>/api/admin/boss/{id}/next</c> (ведущий).
    /// </summary>
    public static IEndpointRouteBuilder MapBoss(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var player = routes.MapGroup("/api/boss/{id:guid}").RequireAuthorization(AuthEndpoints.PlayerPolicy);

        player.MapPost("/act", async (Guid id, BossActRequest body, ClaimsPrincipal user, BossService boss) =>
        {
            await boss.ActAsync(id, user.UserId()!.Value, body.Action, body.TargetId);
            return Results.NoContent();
        });

        player.MapPost("/cheer", async (Guid id, ClaimsPrincipal user, BossService boss) =>
        {
            await boss.CheerAsync(id, user.UserId()!.Value);
            return Results.NoContent();
        });

        routes.MapPost("/api/admin/boss/{id:guid}/auction", async (Guid id, BossService boss) => Results.Ok(new { id = await boss.OpenAuctionAsync(id) }))
            .RequireAuthorization(AuthEndpoints.AdminPolicy);

        routes.MapPost("/api/admin/boss/{id:guid}/next", async (Guid id, int? step, BossService boss) =>
            {
                await boss.NextAsync(id, step);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }
}
