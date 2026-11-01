using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Bosses;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Auction;

/// <summary>Фаза аукциона.</summary>
public enum AuctionPhase
{
    /// <summary>Ждём «Следующий лот».</summary>
    Idle,

    /// <summary>Идут торги.</summary>
    Bidding,

    /// <summary>Лот продан.</summary>
    Sold,

    /// <summary>Лот без ставок.</summary>
    Unsold,

    /// <summary>Лоты кончились.</summary>
    Done,
}

/// <summary>Чем кончились торги по последнему лоту.</summary>
public record AuctionLast(Guid LotId, Guid? UserId, int? Price);

/// <summary>Состояние аукциона в Event.StateJson.</summary>
/// <param name="Queue">Лоты, которые ещё выйдут, по порядку.</param>
/// <param name="Skipped">Лоты, уже раз ушедшие без ставок: второй раз — сняты.</param>
public record AuctionState(
    AuctionPhase Phase,
    IReadOnlyList<Guid> Queue,
    IReadOnlyList<Guid> Skipped,
    Guid? LotId,
    int? Price,
    Guid? LeaderId,
    DateTimeOffset? Deadline,
    AuctionLast? Last);

/// <summary>Участник торгов и его «на руках».</summary>
public record AuctionBalanceDto(Guid UserId, string Login, string? AvatarUrl, int Balance);

/// <summary>Лидер торгов.</summary>
public record AuctionLeaderDto(Guid UserId, string Login, string? AvatarUrl);

/// <summary>Итог последнего лота.</summary>
public record AuctionLastDto(string LotName, string? Login, int? Price, bool Sold);

/// <summary>Покупка в истории.</summary>
public record AuctionSaleDto(string LotName, string Login, int Price);

/// <summary>Детали аукциона.</summary>
public record AuctionDetails(
    AuctionPhase Phase,
    AuctionLotDto? Lot,
    int? Price,
    AuctionLeaderDto? Leader,
    DateTimeOffset? Deadline,
    int Left,
    AuctionLastDto? Last,
    IReadOnlyList<AuctionBalanceDto> Balances,
    int? MyBalance,
    IReadOnlyList<AuctionSaleDto> Sales);

/// <summary>
/// Аукцион: лоты по одному, первая ставка — по стартовой цене, дальше +5/+10/+25; таймер сбрасывается каждой ставкой.
/// Ослики тратятся с «на руках», места в таблице не меняются.
/// </summary>
public class AuctionModule(AppDbContext db, ILiveNotifier live) : IEventModule
{
    /// <summary>Время на первую ставку.</summary>
    public static readonly TimeSpan FirstBidTime = TimeSpan.FromSeconds(30);

    /// <summary>Время после каждой ставки.</summary>
    public static readonly TimeSpan BidTime = TimeSpan.FromSeconds(15);

    /// <summary>Одна блокировка на ставки, действия именинника и тик.</summary>
    internal static readonly SemaphoreSlim Lock = new(1, 1);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Auction;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        var lots = (await LotsForAsync(ev)).OrderBy(l => l.Order).Select(l => l.Id).ToList();
        if (lots.Count == 0)
        {
            throw new EventRuleException("no_lots");
        }

        SetState(ev, new AuctionState(AuctionPhase.Idle, lots, [], null, null, null, null, null));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var lots = await db.AuctionLots.AsNoTracking().ToDictionaryAsync(l => l.Id);
        var balances = await BalancesAsync(db);
        var logins = balances.ToDictionary(b => b.UserId);
        var sales = await db.AuctionSales.AsNoTracking().Where(s => s.EventId == ev.Id).OrderBy(s => s.CreatedAt).ToListAsync();

        AuctionLastDto? last = state.Last is { } l && lots.TryGetValue(l.LotId, out var lastLot)
            ? new AuctionLastDto(lastLot.Name, l.UserId is { } u && logins.TryGetValue(u, out var buyer) ? buyer.Login : null, l.Price, l.UserId is not null)
            : null;

        return new AuctionDetails(
            state.Phase,
            state.LotId is { } lotId && lots.TryGetValue(lotId, out var lot) ? AuctionLotService.ToDto(lot) : null,
            state.Price,
            state.LeaderId is { } leader && logins.TryGetValue(leader, out var who) ? new AuctionLeaderDto(who.UserId, who.Login, who.AvatarUrl) : null,
            state.Deadline,
            state.Queue.Count,
            last,
            balances,
            viewerId is { } me && logins.TryGetValue(me, out var mine) ? mine.Balance : null,
            sales
                .Where(s => lots.ContainsKey(s.LotId) && logins.ContainsKey(s.UserId))
                .Select(s => new AuctionSaleDto(lots[s.LotId].Name, logins[s.UserId].Login, s.Price))
                .ToList());
    }

    /// <inheritdoc/>
    public async Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        await Lock.WaitAsync();
        try
        {
            // Ставка могла прийти, пока движок читал ивент.
            await db.Entry(ev).ReloadAsync();
            var state = StateOf(ev);
            if (ev.Status != EventStatus.Active || state.Phase != AuctionPhase.Bidding || now < state.Deadline)
            {
                return false;
            }

            SetState(ev, await CloseAsync(ev.Id, state, now));
            await db.SaveChangesAsync();
            await live.BroadcastAsync();
            return false;
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev) => Task.FromResult<IReadOnlyList<Ranked<Guid>>>([]);

    internal static AuctionState StateOf(Event ev) => JsonSerializer.Deserialize<AuctionState>(ev.StateJson!, Json)!;

    internal static void SetState(Event ev, AuctionState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);

    /// <summary>
    /// «На руках» у каждого игрока: ослики минус покупки на аукционах.
    /// </summary>
    /// <summary>
    /// Аукцион боя — открытые медалью непроданные лоты этого босса; финальный — все непроданные (в т. ч. неоткрытые призы боссов).
    /// </summary>
    internal static async Task<IReadOnlyList<Guid>> OpenPrizesAsync(AppDbContext db, Event boss)
    {
        var state = BossModule.StateOf(boss);
        if (state.Medal is not { } medal)
        {
            return [];
        }

        var medals = BossRules.Opened(medal).Cast<Medal?>().ToList();
        return await db.AuctionLots
            .Where(l => l.Boss == state.Boss && medals.Contains(l.Medal) && !db.AuctionSales.Any(s => s.LotId == l.Id))
            .Select(l => l.Id)
            .ToListAsync();
    }

    internal static async Task<IReadOnlyList<AuctionBalanceDto>> BalancesAsync(AppDbContext db)
    {
        var donkeys = await db.DonkeyAwards.GroupBy(a => a.UserId).Select(g => new { UserId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.UserId, x => x.Sum);
        var spent = await db.AuctionSales.GroupBy(s => s.UserId).Select(g => new { UserId = g.Key, Sum = g.Sum(s => s.Price) })
            .ToDictionaryAsync(x => x.UserId, x => x.Sum);
        return (await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync())
            .Select(u => new AuctionBalanceDto(u.Id, u.Login, u.ToMe().AvatarUrl, donkeys.GetValueOrDefault(u.Id) - spent.GetValueOrDefault(u.Id)))
            .OrderByDescending(b => b.Balance)
            .ThenBy(b => b.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Время вышло: есть лидер и ему хватает — продано, иначе лот в конец очереди (второй раз — снят).
    /// </summary>
    private async Task<AuctionState> CloseAsync(Guid eventId, AuctionState state, DateTimeOffset now)
    {
        var lotId = state.LotId!.Value;
        // Пока шли торги, лидера могли оштрафовать — без денег покупки нет, лот как без ставок.
        var leaderCash = state.LeaderId is { } id ? (await BalancesAsync(db)).FirstOrDefault(b => b.UserId == id)?.Balance ?? 0 : 0;
        if (state.LeaderId is { } leader && leaderCash >= state.Price)
        {
            db.AuctionSales.Add(new AuctionSale { Id = Guid.NewGuid(), EventId = eventId, LotId = lotId, UserId = leader, Price = state.Price!.Value, CreatedAt = now });
            return state with { Phase = AuctionPhase.Sold, Deadline = null, Last = new AuctionLast(lotId, leader, state.Price) };
        }

        var again = !state.Skipped.Contains(lotId);
        return state with
        {
            Phase = AuctionPhase.Unsold,
            Deadline = null,
            Queue = again ? [.. state.Queue, lotId] : state.Queue,
            Skipped = again ? [.. state.Skipped, lotId] : state.Skipped,
            Last = new AuctionLast(lotId, null, null),
        };
    }

    private async Task<List<AuctionLot>> LotsForAsync(Event ev)
    {
        if (ev.ParentId is { } parentId && await db.Events.FindAsync(parentId) is { Type: EventType.BossFight } boss)
        {
            var open = await OpenPrizesAsync(db, boss);
            return await db.AuctionLots.Where(l => open.Contains(l.Id)).ToListAsync();
        }

        return await db.AuctionLots.Where(l => !db.AuctionSales.Any(s => s.LotId == l.Id)).ToListAsync();
    }
}
