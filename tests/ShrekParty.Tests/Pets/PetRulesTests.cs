using AwesomeAssertions;
using ShrekParty.Api.Pets;

namespace ShrekParty.Tests.Pets;

public class PetRulesTests
{
    [Fact]
    public void PickClass_takes_least_then_random()
    {
        var random = new Random(7);
        var tie = Enumerable.Range(0, 200)
            .Select(_ => PetRules.PickClass(Counts(tank: 4, fighter: 3, mage: 3), random))
            .ToHashSet();

        tie.Should().BeEquivalentTo([PetClass.Fighter, PetClass.Mage]);
        Enumerable.Range(0, 50).Select(_ => PetRules.PickClass(Counts(tank: 1, fighter: 2, mage: 2), random))
            .Should().OnlyContain(c => c == PetClass.Tank);
    }

    [Fact]
    public void Dealing_ten_gives_4_3_3()
    {
        var random = new Random(3);
        var counts = Counts(0, 0, 0);
        for (var i = 0; i < 10; i++)
        {
            counts[PetRules.PickClass(counts, random)]++;
        }

        counts.Values.Order().Should().Equal(3, 3, 4);
    }

    [Theory]
    [InlineData(PetClass.Tank)]
    [InlineData(PetClass.Fighter)]
    [InlineData(PetClass.Mage)]
    public void Roll_lead_six_strictly_greater_sum_twelve(PetClass petClass)
    {
        for (var seed = 0; seed < 300; seed++)
        {
            var start = PetRules.Roll(petClass, new Random(seed));
            var values = Values(start.Stats);
            var lead = values[PetRules.Lead(petClass)];
            var minors = values.Where(v => v.Key != PetRules.Lead(petClass)).Select(v => v.Value).ToList();

            start.Class.Should().Be(petClass);
            lead.Should().Be(6);
            minors.Should().OnlyContain(v => v >= 2 && v <= 4 && v < lead);
            values.Values.Sum().Should().Be(12);
        }
    }

    [Fact]
    public void Roll_first_minor_is_smaller()
    {
        for (var seed = 0; seed < 100; seed++)
        {
            var start = PetRules.Roll(PetClass.Mage, new Random(seed));
            if (start.Stats.Strength != start.Stats.Agility)
            {
                var smaller = start.Stats.Strength < start.Stats.Agility ? PetStat.Strength : PetStat.Agility;
                start.FirstMinor.Should().Be(smaller);
            }
            else
            {
                start.FirstMinor.Should().BeOneOf(PetStat.Strength, PetStat.Agility);
            }
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(120, 3)]
    public void Level_from_earned(int earned, int level)
    {
        PetRules.Level(earned).Should().Be(level);
        PetRules.NextLevelAt(level).Should().Be(level * 50);
    }

    [Fact]
    public void StatsAt_level_three_mage()
    {
        var start = new PetStart(PetClass.Mage, new PetStats(2, 4, 6), PetStat.Strength);

        PetRules.StatsAt(start, 3).Should().Be(new PetStats(3, 5, 10));
    }

    [Fact]
    public void StatsAt_level_two_only_first_minor()
    {
        var start = new PetStart(PetClass.Mage, new PetStats(2, 4, 6), PetStat.Strength);

        PetRules.StatsAt(start, 2).Should().Be(new PetStats(3, 4, 8));
        PetRules.StatsAt(start, 1).Should().Be(start.Stats);
    }

    [Fact]
    public void Derived()
    {
        var stats = new PetStats(4, 5, 6);

        stats.Health.Should().Be(40);
        stats.Damage.Should().Be(7);
        stats.Mana.Should().Be(12);
    }

    private static Dictionary<PetClass, int> Counts(int tank, int fighter, int mage) =>
        new() { [PetClass.Tank] = tank, [PetClass.Fighter] = fighter, [PetClass.Mage] = mage };

    private static Dictionary<PetStat, int> Values(PetStats s) =>
        new() { [PetStat.Strength] = s.Strength, [PetStat.Agility] = s.Agility, [PetStat.Intellect] = s.Intellect };
}
