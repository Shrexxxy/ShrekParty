using ShrekParty.Api.Bosses;

namespace ShrekParty.Api.Auction;

/// <summary>
/// Лот аукциона — контент, заводится заранее и переживает «Сбросить всё».
/// </summary>
public class AuctionLot
{
    public Guid Id { get; set; }

    public int Order { get; set; }

    public required string Name { get; set; }

    public int StartPrice { get; set; }

    public Guid? MediaId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Приз за победу над этим боссом; null — финальный аукцион.</summary>
    public BossKind? Boss { get; set; }

    /// <summary>С какой медали боя лот открывается; задан вместе с боссом.</summary>
    public Medal? Medal { get; set; }
}

/// <summary>
/// Покупка на аукционе: ослики списываются с «на руках», места в таблице не меняются.
/// </summary>
public class AuctionSale
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid LotId { get; set; }

    public Guid UserId { get; set; }

    public int Price { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
