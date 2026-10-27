using AwesomeAssertions;
using ShrekParty.Api.Tournament;

namespace ShrekParty.Tests.Tournament;

public class BracketBuilderTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(11)]
    public void Every_player_starts_exactly_once(int n)
    {
        var players = Players(n);

        var plan = BracketBuilder.Build(players, new Random(1));

        var firstStage = plan.Where(m => m.Round == TournamentRound.Qualifier || m.Round == plan.FirstMainRound()).ToList();
        var starters = firstStage.SelectMany(m => new[] { m.PlayerA, m.PlayerB }).OfType<Guid>().ToList();
        starters.Should().BeEquivalentTo(players);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    public void Qualifiers_only_above_eight(int n, int qualifiers)
    {
        var plan = BracketBuilder.Build(Players(n), new Random(1));

        plan.Count(m => m.Round == TournamentRound.Qualifier).Should().Be(qualifiers);
        plan.Count(m => m.Round == TournamentRound.Quarter).Should().Be(4);
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(6, 2)]
    [InlineData(7, 1)]
    [InlineData(8, 0)]
    [InlineData(3, 1)]
    public void Byes_fill_missing_places(int n, int byes)
    {
        var plan = BracketBuilder.Build(Players(n), new Random(1));

        plan.Count(m => m.IsBye).Should().Be(byes);
        plan.Where(m => m.IsBye).Should().OnlyContain(m => m.PlayerA != null && m.PlayerB == null);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(11)]
    public void Semifinal_losers_go_to_third_place(int n)
    {
        var plan = BracketBuilder.Build(Players(n), new Random(1));

        var third = plan.Single(m => m.Round == TournamentRound.ThirdPlace);
        plan.Where(m => m.Round == TournamentRound.Semi).Should().OnlyContain(m => m.LoserNextIndex == third.Index);
        plan.Single(m => m.Round == TournamentRound.Final).NextIndex.Should().BeNull();
    }

    [Fact]
    public void Two_players_play_only_final()
    {
        var plan = BracketBuilder.Build(Players(2), new Random(1));

        plan.Select(m => m.Round).Should().Equal(TournamentRound.Final);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(11)]
    public void Every_match_except_last_ones_feeds_next(int n)
    {
        var plan = BracketBuilder.Build(Players(n), new Random(1));

        plan.Where(m => m.Round is not (TournamentRound.Final or TournamentRound.ThirdPlace))
            .Should().OnlyContain(m => m.NextIndex != null && m.NextSlot != null);
    }

    [Fact]
    public void Same_seed_same_bracket()
    {
        var players = Players(9);

        var a = BracketBuilder.Build(players, new Random(42));
        var b = BracketBuilder.Build(players, new Random(42));

        a.Should().BeEquivalentTo(b);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void Unsupported_player_count_throws(int n)
    {
        var act = () => BracketBuilder.Build(Players(n), new Random(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(9, new[] { 1, 2, 3, 4, 5, 5, 5, 5, 6 })]
    [InlineData(8, new[] { 1, 2, 3, 4, 5, 5, 5, 5 })]
    [InlineData(6, new[] { 1, 2, 3, 4, 5, 5 })]
    [InlineData(3, new[] { 1, 2, 3 })]
    [InlineData(2, new[] { 1, 2 })]
    public void Ranking_of_finished_bracket(int n, int[] places)
    {
        var plan = BracketBuilder.Build(Players(n), new Random(1));

        var results = PlayOut(plan);

        TournamentRanking.Rank(results).Select(r => r.Place).Order().Should().Equal(places);
    }

    [Fact]
    public void Ranking_puts_final_winner_first()
    {
        var plan = BracketBuilder.Build(Players(9), new Random(1));
        var results = PlayOut(plan);
        var final = results.Single(r => r.Round == TournamentRound.Final);

        var ranking = TournamentRanking.Rank(results);

        ranking.Single(r => r.Place == 1).Item.Should().Be(final.Winner!.Value);
        ranking.Single(r => r.Place == 2).Item.Should().Be(final.Loser!.Value);
    }

    private static List<Guid> Players(int n) => Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToList();

    /// <summary>Разыгрывает сетку: всегда побеждает игрок A, bye — проходит единственный.</summary>
    private static List<MatchResult> PlayOut(IReadOnlyList<PlannedMatch> plan)
    {
        var slots = plan.ToDictionary(m => m.Index, m => (A: m.PlayerA, B: m.PlayerB));
        var results = new List<MatchResult>();
        foreach (var match in plan.OrderBy(m => m.Index))
        {
            var (a, b) = slots[match.Index];
            var winner = a ?? b;
            var loser = match.IsBye ? null : b;
            results.Add(new MatchResult(match.Round, winner, loser));
            Put(slots, match.NextIndex, match.NextSlot, winner);
            Put(slots, match.LoserNextIndex, match.LoserNextSlot, loser);
        }

        return results;
    }

    private static void Put(Dictionary<int, (Guid? A, Guid? B)> slots, int? index, MatchSlot? slot, Guid? player)
    {
        if (index is null || player is null)
        {
            return;
        }

        var current = slots[index.Value];
        slots[index.Value] = slot == MatchSlot.A ? (player, current.B) : (current.A, player);
    }
}
