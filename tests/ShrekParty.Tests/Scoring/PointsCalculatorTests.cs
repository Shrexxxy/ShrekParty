using AwesomeAssertions;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Tests.Scoring;

public class PointsCalculatorTests
{
    [Fact]
    public void Linear_nine_players_max_100()
    {
        var points = Enumerable.Range(1, 9)
            .Select(place => PointsCalculator.ForPlace(ScoringScheme.Linear, place, distinctPlaces: 9, maxDonkeys: 100));

        points.Should().Equal(100, 89, 78, 67, 56, 44, 33, 22, 11);
    }

    [Fact]
    public void Linear_two_places_max_10()
    {
        PointsCalculator.ForPlace(ScoringScheme.Linear, 1, distinctPlaces: 2, maxDonkeys: 10).Should().Be(10);
        PointsCalculator.ForPlace(ScoringScheme.Linear, 2, distinctPlaces: 2, maxDonkeys: 10).Should().Be(5);
    }

    [Fact]
    public void Podium_nine_places_max_100()
    {
        var points = Enumerable.Range(1, 9)
            .Select(place => PointsCalculator.ForPlace(ScoringScheme.Podium, place, distinctPlaces: 9, maxDonkeys: 100));

        points.Should().Equal(100, 70, 50, 40, 33, 27, 20, 13, 7);
    }

    [Fact]
    public void Podium_two_places()
    {
        PointsCalculator.ForPlace(ScoringScheme.Podium, 1, distinctPlaces: 2, maxDonkeys: 100).Should().Be(100);
        PointsCalculator.ForPlace(ScoringScheme.Podium, 2, distinctPlaces: 2, maxDonkeys: 100).Should().Be(70);
    }

    [Fact]
    public void Custom_uses_table_and_zero_for_missing()
    {
        var table = new Dictionary<int, int> { [1] = 15, [2] = 5 };

        PointsCalculator.ForPlace(ScoringScheme.Custom, 1, distinctPlaces: 3, maxDonkeys: 20, table).Should().Be(15);
        PointsCalculator.ForPlace(ScoringScheme.Custom, 3, distinctPlaces: 3, maxDonkeys: 20, table).Should().Be(0);
    }
}
