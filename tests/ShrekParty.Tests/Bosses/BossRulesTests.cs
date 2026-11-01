using AwesomeAssertions;
using ShrekParty.Api.Bosses;
using ShrekParty.Api.Pets;

namespace ShrekParty.Tests.Bosses;

public class BossRulesTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();
    private static readonly Guid D = Guid.NewGuid();

    [Fact]
    public void MaxHp_is_k_times_attacks()
    {
        BossRules.MaxHp(BossKind.Farquaad, [Pet(A, damage: 6), Pet(B, damage: 8)]).Should().Be(7 * 14);
        BossRules.MaxHp(BossKind.Peasant, [Pet(A, damage: 6)]).Should().Be(6 * 6);
        BossRules.MaxHp(BossKind.Fairy, [Pet(A, damage: 6)]).Should().Be(8 * 6);
    }

    [Fact]
    public void Attack_and_power_and_fire()
    {
        var fighters = new[] { Pet(A, damage: 6), Pet(B, PetClass.Fighter, damage: 7), Pet(C, PetClass.Mage, intellect: 5, mana: 10) };
        var choices = Choices((A, FighterAction.Attack), (B, FighterAction.Power), (C, FighterAction.Fire));

        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.Kvass), fighters, choices, bossHp: 500);

        Amount(result, FxKind.Attack, A).Should().Be(6);
        Amount(result, FxKind.Power, B).Should().Be(14);
        Amount(result, FxKind.Fire, C).Should().Be(15);
        result.Fighters.Single(f => f.UserId == C).Mana.Should().Be(10 - 4 + 1);
    }

    [Fact]
    public void Shield_halves_damage_rounded_up()
    {
        var result = Resolve(BossKind.Farquaad, Intent(BossMoveKind.Shield), [Pet(A, damage: 7)], Choices((A, FighterAction.Attack)), bossHp: 100);

        Amount(result, FxKind.Attack, A).Should().Be(4);
        result.BossHp.Should().Be(96);
    }

    [Fact]
    public void Not_chosen_hits_half()
    {
        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.Kvass), [Pet(A, damage: 7)], Choices([]), bossHp: 100);

        Amount(result, FxKind.Weak, A).Should().Be(4);
    }

    [Fact]
    public void Cheers_add_up_to_three()
    {
        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.CallNeighbours), [Pet(A, damage: 6)], Choices((A, FighterAction.Attack)), bossHp: 100, cheers: 5);

        Amount(result, FxKind.Attack, A).Should().Be(9);
    }

    [Fact]
    public void Dodge_avoids_hit_and_not_twice()
    {
        var fighters = new[] { Pet(A), Pet(B) };
        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.Poke, A), fighters, Choices((A, FighterAction.Dodge), (B, FighterAction.Attack)), bossHp: 100);

        result.Feed.Should().Contain(f => f.Kind == FxKind.Dodged && f.TargetId == A);
        result.Fighters.Single(f => f.UserId == A).Hp.Should().Be(50);
        result.Fighters.Single(f => f.UserId == A).DodgedLast.Should().BeTrue();
        BossRules.Unavailable(result.Fighters.Single(f => f.UserId == A), FighterAction.Dodge).Should().Be("dodge_twice");
    }

    [Fact]
    public void Power_cannot_dodge_takes_hit()
    {
        var fighters = new[] { Pet(A, PetClass.Fighter) };
        var hit = BossRules.Hit(BossKind.Peasant, BossMoveKind.Poke, fighters, turn: 1, bossHp: 100, bossMax: 100, doubled: false);

        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.Poke, A), fighters, Choices((A, FighterAction.Power)), bossHp: 1000);

        result.Fighters.Single().Hp.Should().Be(Math.Max(0, 50 - hit));
    }

    [Fact]
    public void Hit_formula()
    {
        // Средний максимум здоровья 50 → тычок 0,9 × 50 × 2,0 = 90.
        BossRules.Hit(BossKind.Peasant, BossMoveKind.Poke, [Pet(A), Pet(B)], 1, 100, 100, false).Should().Be(90);
        BossRules.Hit(BossKind.Peasant, BossMoveKind.Poke, [Pet(A), Pet(B)], 1, 100, 100, doubled: true).Should().Be(180);
        BossRules.Hit(BossKind.Peasant, BossMoveKind.Kvass, [Pet(A)], 1, 100, 100, false).Should().Be(0);
    }

    [Fact]
    public void Rage_from_turn_nine()
    {
        var fighters = new[] { Pet(A) };
        var normal = BossRules.Hit(BossKind.Farquaad, BossMoveKind.Volley, fighters, 8, 100, 100, false);

        // 0,30 × 50 × 2,6 = 39; на 10-м ходу ×1,3 = 50,7 → 51.
        normal.Should().Be(39);
        BossRules.Hit(BossKind.Farquaad, BossMoveKind.Volley, fighters, 10, 100, 100, false).Should().Be(51);
    }

    [Fact]
    public void Fairy_rage_below_third()
    {
        var fighters = new[] { Pet(A) };

        // 0,45 × 50 × 1,4 = 31,5 → 32; ниже трети здоровья ×1,5 = 47,25 → 48.
        BossRules.Hit(BossKind.Fairy, BossMoveKind.Stars, fighters, 1, 30, 100, false).Should().Be(48);
        BossRules.Hit(BossKind.Fairy, BossMoveKind.Stars, fighters, 1, 40, 100, false).Should().Be(32);
    }

    [Fact]
    public void Cover_takes_all_halved_shared()
    {
        // Танки толстые, чтобы пережить половину всего залпа; урон считаем той же формулой.
        var fighters = new[] { Pet(A, PetClass.Tank, hp: 5000), Pet(B, hp: 200), Pet(C, hp: 200), Pet(D, hp: 200) };
        var intent = Intent(BossMoveKind.Volley, A, B, C, D);
        var hit = BossRules.Hit(BossKind.Farquaad, BossMoveKind.Volley, fighters, 1, 1000, 1000, false);

        var one = Resolve(BossKind.Farquaad, intent, fighters, Choices((A, FighterAction.Cover)), bossHp: 100_000);

        one.Fighters.Single(f => f.UserId == A).Hp.Should().Be(5000 - (int)Math.Ceiling(hit * 4 / 2.0));
        one.Fighters.Where(f => f.UserId != A).Should().OnlyContain(f => f.Hp == 200);
        one.Feed.Should().Contain(f => f.Kind == FxKind.Cover && f.ActorId == A);

        var twoTanks = new[] { Pet(A, PetClass.Tank, hp: 5000), Pet(B, PetClass.Tank, hp: 5000), Pet(C, hp: 200), Pet(D, hp: 200) };
        var hit2 = BossRules.Hit(BossKind.Farquaad, BossMoveKind.Volley, twoTanks, 1, 1000, 1000, false);
        var two = Resolve(BossKind.Farquaad, intent, twoTanks, Choices((A, FighterAction.Cover), (B, FighterAction.Cover)), bossHp: 100_000);
        var share = (int)Math.Ceiling(hit2 * 4 / 2.0 / 2);
        two.Fighters.Where(f => f.UserId == A || f.UserId == B).Should().OnlyContain(f => f.Hp == 5000 - share);
    }

    [Fact]
    public void Cover_halves_hit_on_tank_himself()
    {
        var fighters = new[] { Pet(A, PetClass.Tank, hp: 5000), Pet(B, hp: 200) };
        var hit = BossRules.Hit(BossKind.Farquaad, BossMoveKind.Sword, fighters, 1, 1000, 1000, false);

        var result = Resolve(BossKind.Farquaad, Intent(BossMoveKind.Sword, A), fighters, Choices((A, FighterAction.Cover)), bossHp: 100_000);

        result.Fighters.Single(f => f.UserId == A).Hp.Should().Be(5000 - (int)Math.Ceiling(hit / 2.0));
    }

    [Fact]
    public void Heal_before_hit_capped()
    {
        var fighters = new[] { Pet(A, PetClass.Mage, intellect: 5, mana: 6), Pet(B, hp: 45) };

        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.Kvass), fighters, Targeted((A, FighterAction.Heal, B)), bossHp: 100);

        result.Fighters.Single(f => f.UserId == B).Hp.Should().Be(50);
        Amount(result, FxKind.Heal, A).Should().Be(5);
        result.Fighters.Single(f => f.UserId == A).Mana.Should().Be(6 - 3 + 1);
    }

    [Fact]
    public void Win_checked_before_boss_hits()
    {
        var fighters = new[] { Pet(A, damage: 8, hp: 1) };

        var result = Resolve(BossKind.Farquaad, Intent(BossMoveKind.Volley, A), fighters, Choices((A, FighterAction.Attack)), bossHp: 5);

        result.BossHp.Should().Be(0);
        result.Fighters.Single().Alive.Should().BeTrue();
        result.Feed.Should().NotContain(f => f.Kind == FxKind.BossHit || f.Kind == FxKind.Knockout);
    }

    [Fact]
    public void Pumpkin_stuns_unless_dodged_and_stunned_skips_turn()
    {
        var fighters = new[] { Pet(A, hp: 500), Pet(B, hp: 500) };
        var intent = Intent(BossMoveKind.Pumpkin, A, B);

        var result = Resolve(BossKind.Fairy, intent, fighters, Choices((A, FighterAction.Dodge), (B, FighterAction.Attack)), bossHp: 1000);

        result.Fighters.Single(f => f.UserId == A).Stunned.Should().BeFalse();
        result.Fighters.Single(f => f.UserId == B).Stunned.Should().BeTrue();
        BossRules.Unavailable(result.Fighters.Single(f => f.UserId == B), FighterAction.Attack).Should().Be("stunned");

        var next = Resolve(BossKind.Fairy, Intent(BossMoveKind.Potion), result.Fighters, Choices((B, FighterAction.Attack)), bossHp: 1000);
        next.Feed.Should().Contain(f => f.Kind == FxKind.Stunned && f.ActorId == B);
        next.Feed.Should().NotContain(f => f.ActorId == B && f.Kind == FxKind.Attack);
        next.Fighters.Single(f => f.UserId == B).Stunned.Should().BeFalse();
    }

    [Fact]
    public void Windup_doubles_next()
    {
        var result = Resolve(BossKind.Farquaad, Intent(BossMoveKind.Windup), [Pet(A)], Choices([]), bossHp: 1000);

        result.NextDoubled.Should().BeTrue();
        result.Feed.Should().Contain(f => f.Kind == FxKind.BossWindup);
    }

    [Fact]
    public void Kvass_and_potion_heal_boss_capped()
    {
        var kvass = Resolve(BossKind.Peasant, Intent(BossMoveKind.Kvass), [Pet(A, damage: 0)], Choices([]), bossHp: 50, bossMax: 100);
        kvass.BossHp.Should().Be(58);
        kvass.Feed.Should().Contain(f => f.Kind == FxKind.BossHeal && f.Amount == 8);

        var potion = Resolve(BossKind.Fairy, Intent(BossMoveKind.Potion), [Pet(A, damage: 0)], Choices([]), bossHp: 95, bossMax: 100);
        potion.BossHp.Should().Be(100);
    }

    [Fact]
    public void Knockout_and_mana_regen_and_feed_order()
    {
        var fighters = new[] { Pet(A, PetClass.Mage, intellect: 4, mana: 3, maxMana: 8), Pet(B, hp: 1) };

        var result = Resolve(BossKind.Peasant, Intent(BossMoveKind.Poke, B), fighters, Targeted((A, FighterAction.Heal, A), (B, FighterAction.Attack, B)), bossHp: 1000);

        result.Fighters.Single(f => f.UserId == B).Should().Match<Fighter>(f => !f.Alive && f.Hp == 0);
        result.Fighters.Single(f => f.UserId == A).Mana.Should().Be(1);
        result.Feed.Select(f => f.Kind).Should().Equal(FxKind.Heal, FxKind.Attack, FxKind.BossHit, FxKind.Knockout);
    }

    [Fact]
    public void Plan_targets_alive_only()
    {
        var fighters = new[] { Pet(A), Pet(B, hp: 0), Pet(C) };
        for (var seed = 0; seed < 200; seed++)
        {
            var intent = BossRules.Plan(BossKind.Peasant, fighters, new Random(seed));
            intent.Targets.Should().NotContain(B);
            if (intent.Move == BossMoveKind.Swing)
            {
                intent.Targets.Should().HaveCount(2).And.OnlyHaveUniqueItems();
            }
        }
    }

    [Theory]
    [InlineData(7, 10, Medal.Gold)]
    [InlineData(4, 10, Medal.Silver)]
    [InlineData(3, 10, Medal.Bronze)]
    public void Medal_by_share(int alive, int total, Medal medal) => BossRules.MedalFor(alive, total).Should().Be(medal);

    [Fact]
    public void Unavailable_by_class_and_mana()
    {
        BossRules.Unavailable(Pet(A, PetClass.Fighter), FighterAction.Cover).Should().Be("bad_action");
        BossRules.Unavailable(Pet(A, PetClass.Mage, mana: 3), FighterAction.Fire).Should().Be("no_mana");
        BossRules.Unavailable(Pet(A, PetClass.Mage, mana: 3), FighterAction.Heal).Should().BeNull();
        BossRules.Unavailable(Pet(A, hp: 0), FighterAction.Attack).Should().Be("dead");
    }

    private static Fighter Pet(Guid id, PetClass cls = PetClass.Fighter, int hp = 50, int damage = 5, int intellect = 3, int mana = 6, int maxMana = 10) =>
        new(id, cls, hp, 50 > hp ? 50 : hp, mana, maxMana, damage, intellect, DodgedLast: false, Stunned: false);

    private static BossIntent Intent(BossMoveKind move, params Guid[] targets) => new(move, targets);

    private static Dictionary<Guid, FighterChoice> Choices(params (Guid Id, FighterAction Action)[] choices) =>
        choices.ToDictionary(c => c.Id, c => new FighterChoice(c.Action, null));

    private static Dictionary<Guid, FighterChoice> Targeted(params (Guid Id, FighterAction Action, Guid Target)[] choices) =>
        choices.ToDictionary(c => c.Id, c => new FighterChoice(c.Action, c.Target));

    private static TurnResult Resolve(
        BossKind boss, BossIntent intent, IReadOnlyList<Fighter> fighters, IReadOnlyDictionary<Guid, FighterChoice> choices, int bossHp, int bossMax = 1000, int cheers = 0) =>
        BossRules.Resolve(boss, intent, fighters, choices, bossHp, bossMax, turn: 1, doubled: false, cheers);

    private static int Amount(TurnResult result, FxKind kind, Guid actor) => result.Feed.Single(f => f.Kind == kind && f.ActorId == actor).Amount;
}
