using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Auction;

/// <summary>Ставка: 0 — первая (по стартовой цене), иначе шаг +5/+10/+25.</summary>
/// <param name="Expected">Цена, которую видел игрок, когда жал; кто-то успел раньше — <c>price_changed</c>, а не дороже, чем он думал.</param>
public record AuctionBidRequest(int Step, int? Expected = null);

/// <summary>
/// Действия аукциона: следующий лот и отмена продажи (именинник), ставки (игроки).
/// </summary>
public class AuctionService(AppDbContext db, ILiveNotifier live, TimeProvider time)
{
    /// <summary>Разрешённые шаги ставки.</summary>
    public static readonly int[] Steps = [5, 10, 25];

    /// <summary>
    /// Следующий лот на торги; лотов нет — аукцион окончен.
    /// </summary>
    public Task NextAsync(Guid eventId) => ChangeAsync(eventId, state =>
    {
        if (state.Phase is AuctionPhase.Bidding or AuctionPhase.Done)
        {
            throw new EventRuleException("not_idle");
        }
        if (state.Queue.Count == 0)
        {
            return Task.FromResult(state with { Phase = AuctionPhase.Done, LotId = null, Price = null, LeaderId = null });
        }

        return Task.FromResult(state with
        {
            Phase = AuctionPhase.Bidding,
            LotId = state.Queue[0],
            Queue = state.Queue.Skip(1).ToList(),
            Price = null,
            LeaderId = null,
            Deadline = time.GetUtcNow() + AuctionModule.FirstBidTime,
        });
    });

    /// <summary>
    /// Ставка игрока: первая — по стартовой цене, дальше шаг; лидер сам себя не перебивает; не больше «на руках».
    /// </summary>
    public Task BidAsync(Guid eventId, Guid userId, int step, int? expected = null) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != AuctionPhase.Bidding || time.GetUtcNow() >= state.Deadline)
        {
            throw new EventRuleException("too_late");
        }
        if (state.LeaderId == userId)
        {
            throw new EventRuleException("own_lead");
        }

        // Жал «Беру за старт» или «+5» по цене, которой уже нет: двое нажали одновременно.
        if (state.Price is not null && (step == 0 || expected is not null && expected != state.Price))
        {
            throw new EventRuleException("price_changed");
        }

        int price;
        if (state.Price is { } current)
        {
            price = Steps.Contains(step) ? current + step : throw new BadInputException("bad_step");
        }
        else
        {
            price = (await db.AuctionLots.FirstAsync(l => l.Id == state.LotId)).StartPrice;
        }

        var balance = (await AuctionModule.BalancesAsync(db)).FirstOrDefault(b => b.UserId == userId)?.Balance ?? 0;
        if (price > balance)
        {
            throw new EventRuleException("balance");
        }

        return state with { Price = price, LeaderId = userId, Deadline = time.GetUtcNow() + AuctionModule.BidTime };
    });

    /// <summary>
    /// Отмена продажи (ошиблись): ослики возвращаются, лот первым в очередь.
    /// </summary>
    public Task UndoAsync(Guid eventId) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != AuctionPhase.Sold || state.Last is not { } last)
        {
            throw new EventRuleException("not_sold");
        }

        var sale = await db.AuctionSales.Where(s => s.EventId == eventId && s.LotId == last.LotId).OrderByDescending(s => s.CreatedAt).FirstAsync();
        db.AuctionSales.Remove(sale);
        return state with { Phase = AuctionPhase.Idle, Queue = [last.LotId, .. state.Queue], Last = null, LotId = null, Price = null, LeaderId = null };
    });

    private async Task ChangeAsync(Guid eventId, Func<AuctionState, Task<AuctionState>> change)
    {
        await AuctionModule.Lock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.Auction);
            AuctionModule.SetState(ev, await change(AuctionModule.StateOf(ev)));
            await db.SaveChangesAsync();
        }
        finally
        {
            AuctionModule.Lock.Release();
        }

        await live.BroadcastAsync();
    }
}

/// <summary>
/// Маршруты торгов.
/// </summary>
public static class AuctionEndpoints
{
    /// <summary>
    /// <c>/api/auction/{id}/bid</c> (игрок), <c>/api/admin/auction/{id}/next|undo</c> (именинник).
    /// </summary>
    public static IEndpointRouteBuilder MapAuction(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();

        routes.MapPost("/api/auction/{id:guid}/bid", async (Guid id, AuctionBidRequest body, ClaimsPrincipal user, AuctionService auction) =>
            {
                await auction.BidAsync(id, user.UserId()!.Value, body.Step, body.Expected);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        var admin = routes.MapGroup("/api/admin/auction/{id:guid}").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapPost("/next", async (Guid id, AuctionService auction) =>
        {
            await auction.NextAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/undo", async (Guid id, AuctionService auction) =>
        {
            await auction.UndoAsync(id);
            return Results.NoContent();
        });

        return app;
    }
}
