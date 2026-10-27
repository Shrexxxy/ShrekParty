using AwesomeAssertions;
using ShrekParty.Api.Tote;

namespace ShrekParty.Tests.Tote;

public class PariMutuelTests
{
    private static readonly Guid X = Guid.NewGuid();
    private static readonly Guid Y = Guid.NewGuid();

    [Fact]
    public void Splits_pool_proportionally_between_winners()
    {
        var a = Bet(X, 10);
        var b = Bet(X, 30);
        var c = Bet(Y, 20);

        var payouts = PariMutuel.Settle([a, b, c], winnerId: X);

        payouts.Should().BeEquivalentTo(new Dictionary<Guid, int> { [a.BetId] = 15, [b.BetId] = 45 });
    }

    [Fact]
    public void Refunds_everyone_when_nobody_won()
    {
        var a = Bet(Y, 10);
        var b = Bet(Y, 5);

        var payouts = PariMutuel.Settle([a, b], winnerId: X);

        payouts.Should().BeEquivalentTo(new Dictionary<Guid, int> { [a.BetId] = 10, [b.BetId] = 5 });
    }

    [Fact]
    public void Rounding_remainder_goes_to_biggest_winning_bet()
    {
        var a = Bet(X, 10);
        var b = Bet(X, 20);
        var c = Bet(Y, 10);

        var payouts = PariMutuel.Settle([a, b, c], winnerId: X);

        payouts[a.BetId].Should().Be(13);
        payouts[b.BetId].Should().Be(27);
        payouts.Values.Sum().Should().Be(40);
    }

    [Fact]
    public void No_bets_no_payouts() => PariMutuel.Settle([], X).Should().BeEmpty();

    [Fact]
    public void Odds_are_pool_over_side()
    {
        var odds = PariMutuel.Odds([Bet(X, 10), Bet(X, 30), Bet(Y, 20)], X);

        odds.Should().Be(1.5);
        PariMutuel.Odds([Bet(X, 10)], Y).Should().BeNull();
    }

    private static PariMutuelBet Bet(Guid on, int amount) => new(Guid.NewGuid(), on, amount);
}
