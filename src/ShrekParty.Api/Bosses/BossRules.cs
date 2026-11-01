using ShrekParty.Api.Pets;

namespace ShrekParty.Api.Bosses;

/// <summary>Босс — по сложности.</summary>
public enum BossKind
{
    /// <summary>Крестьянин с вилами — учебный.</summary>
    Peasant,

    /// <summary>Лорд Фаркуад.</summary>
    Farquaad,

    /// <summary>Фея-крёстная — финальная.</summary>
    Fairy,
}

/// <summary>Ход босса.</summary>
public enum BossMoveKind
{
    /// <summary>Крестьянин: тычок вилами — один объявленный.</summary>
    Poke,

    /// <summary>Крестьянин: размах — двое случайных.</summary>
    Swing,

    /// <summary>Крестьянин: пьёт квас — лечит себя.</summary>
    Kvass,

    /// <summary>Крестьянин: зовёт соседей — следующий удар двойной.</summary>
    CallNeighbours,

    /// <summary>Фаркуад: удар мечом — один объявленный.</summary>
    Sword,

    /// <summary>Фаркуад: залп лучников — все.</summary>
    Volley,

    /// <summary>Фаркуад: щит — урон по нему вдвое меньше.</summary>
    Shield,

    /// <summary>Фаркуад: замах — следующий удар двойной.</summary>
    Windup,

    /// <summary>Фея: волшебная палочка — один объявленный.</summary>
    Wand,

    /// <summary>Фея: звёздный дождь — все.</summary>
    Stars,

    /// <summary>Фея: в тыкву — цель пропускает следующий ход.</summary>
    Pumpkin,

    /// <summary>Фея: зелье — лечит себя.</summary>
    Potion,
}

/// <summary>Действие питомца в ход.</summary>
public enum FighterAction
{
    /// <summary>Атака: 2 + ловкость.</summary>
    Attack,

    /// <summary>Уклон: удар босса мимо; два хода подряд нельзя.</summary>
    Dodge,

    /// <summary>Заслон (танк): весь урон по команде на себя, вдвое меньше.</summary>
    Cover,

    /// <summary>Мощный удар (боец): двойной урон, уклониться нельзя.</summary>
    Power,

    /// <summary>Огненный шар (маг): 4 маны, 3 × интеллект.</summary>
    Fire,

    /// <summary>Лечение (маг): 3 маны, +2 × интеллект союзнику.</summary>
    Heal,
}

/// <summary>Событие ленты хода — одна анимация на экранах.</summary>
public enum FxKind
{
    /// <summary>Маг лечит союзника.</summary>
    Heal,

    /// <summary>Танк встаёт в заслон.</summary>
    Cover,

    /// <summary>Атака.</summary>
    Attack,

    /// <summary>Не выбрал — удар вполсилы.</summary>
    Weak,

    /// <summary>Мощный удар бойца.</summary>
    Power,

    /// <summary>Огненный шар мага.</summary>
    Fire,

    /// <summary>Тыква: наложена или пропускает ход.</summary>
    Stunned,

    /// <summary>Босс бьёт питомца.</summary>
    BossHit,

    /// <summary>Питомец увернулся.</summary>
    Dodged,

    /// <summary>Танк принял удар за команду.</summary>
    Covered,

    /// <summary>Босс лечится.</summary>
    BossHeal,

    /// <summary>Босс поднял щит.</summary>
    BossShield,

    /// <summary>Босс замахнулся — следующий удар двойной.</summary>
    BossWindup,

    /// <summary>Питомец выбыл.</summary>
    Knockout,
}

/// <summary>Медаль за победу — по доле выживших.</summary>
public enum Medal
{
    /// <summary>Выжило меньше трети.</summary>
    Bronze,

    /// <summary>Выжила треть и больше.</summary>
    Silver,

    /// <summary>Выжило две трети и больше.</summary>
    Gold,
}

/// <summary>Питомец в бою.</summary>
/// <param name="Damage">Урон обычной атаки: 2 + ловкость.</param>
/// <param name="DodgedLast">Уклонялся в прошлый ход — сейчас нельзя.</param>
/// <param name="Stunned">Тыква: следующий ход пропускает.</param>
public record Fighter(Guid UserId, PetClass Class, int Hp, int MaxHp, int Mana, int MaxMana, int Damage, int Intellect, bool DodgedLast, bool Stunned)
{
    /// <summary>Ещё в бою.</summary>
    public bool Alive => Hp > 0;
}

/// <summary>Объявленный ход босса и его цели.</summary>
public record BossIntent(BossMoveKind Move, IReadOnlyList<Guid> Targets);

/// <summary>Выбор игрока; цель — только для лечения.</summary>
public record FighterChoice(FighterAction Action, Guid? TargetId);

/// <summary>Событие ленты: кто, кому, сколько.</summary>
public record Fx(FxKind Kind, Guid? ActorId, Guid? TargetId, int Amount);

/// <summary>Итог хода.</summary>
/// <param name="NextDoubled">Босс замахнулся — следующий удар двойной.</param>
public record TurnResult(IReadOnlyList<Fighter> Fighters, int BossHp, bool NextDoubled, IReadOnlyList<Fx> Feed);

/// <summary>
/// Правила боя с боссом: здоровье и ходы босса, действия питомцев, разыгрывание хода в ленту событий.
/// Все множители — из симуляции баланса; подкручиваются после репетиции.
/// </summary>
public static class BossRules
{
    /// <summary>Поддержка с того света: не больше +3 к удару.</summary>
    public const int MaxCheerBonus = 3;

    /// <summary>С этого хода босс в ярости.</summary>
    public const int RageFromTurn = 9;

    private const int FireMana = 4;
    private const int HealMana = 3;
    private const decimal RagePerTurn = 0.15m;
    private const decimal FairyLowHpRage = 1.5m;

    private static readonly Dictionary<BossKind, BossDef> Bosses = new()
    {
        [BossKind.Peasant] = new(6, 2.0m,
        [
            new(BossMoveKind.Poke, 35, 0.9m, Target.One),
            new(BossMoveKind.Swing, 30, 0.6m, Target.Two),
            new(BossMoveKind.Kvass, 15, 0m, Target.None, SelfHeal: 0.08m),
            new(BossMoveKind.CallNeighbours, 20, 0m, Target.None, Windup: true),
        ]),
        [BossKind.Farquaad] = new(7, 2.6m,
        [
            new(BossMoveKind.Sword, 35, 0.75m, Target.One),
            new(BossMoveKind.Volley, 30, 0.30m, Target.All),
            new(BossMoveKind.Shield, 15, 0m, Target.None),
            new(BossMoveKind.Windup, 20, 0m, Target.None, Windup: true),
        ]),
        [BossKind.Fairy] = new(8, 1.4m,
        [
            new(BossMoveKind.Wand, 30, 1.2m, Target.One),
            new(BossMoveKind.Stars, 35, 0.45m, Target.All),
            new(BossMoveKind.Pumpkin, 20, 0.3m, Target.One, Stun: true),
            new(BossMoveKind.Potion, 15, 0m, Target.None, SelfHeal: 0.10m),
        ]),
    };

    /// <summary>Здоровье босса: K × сумма обычных атак всех участников.</summary>
    public static int MaxHp(BossKind boss, IReadOnlyList<Fighter> fighters) => Bosses[boss].HpPerAttack * fighters.Sum(f => f.Damage);

    /// <summary>Следующий ход босса — случайно по весам; цели — среди живых.</summary>
    public static BossIntent Plan(BossKind boss, IReadOnlyList<Fighter> fighters, Random random)
    {
        var moves = Bosses[boss].Moves;
        var roll = random.Next(moves.Sum(m => m.Weight));
        var move = moves.First(m => (roll -= m.Weight) < 0);
        var alive = fighters.Where(f => f.Alive).Select(f => f.UserId).ToArray();
        random.Shuffle(alive);
        var targets = move.Target switch
        {
            Target.One => alive.Take(1),
            Target.Two => alive.Take(2),
            Target.All => alive,
            _ => [],
        };
        return new BossIntent(move.Kind, targets.ToList());
    }

    /// <summary>
    /// Урон хода по одной цели: множитель × среднее максимальное здоровье команды × сила босса × ярость, вверх.
    /// </summary>
    public static int Hit(BossKind boss, BossMoveKind move, IReadOnlyList<Fighter> fighters, int turn, int bossHp, int bossMax, bool doubled)
    {
        var def = Bosses[boss];
        var mult = def.Moves.First(m => m.Kind == move).Multiplier;
        if (mult == 0 || fighters.Count == 0)
        {
            return 0;
        }

        var rage = 1 + RagePerTurn * Math.Max(0, turn - (RageFromTurn - 1));
        if (boss == BossKind.Fairy && bossHp * 3 < bossMax)
        {
            rage *= FairyLowHpRage;
        }

        var average = (decimal)fighters.Sum(f => f.MaxHp) / fighters.Count;
        return (int)Math.Ceiling(mult * average * def.Strength * rage * (doubled ? 2 : 1));
    }

    /// <summary>Почему действие сейчас нельзя: null — можно.</summary>
    public static string? Unavailable(Fighter fighter, FighterAction action) => (fighter, action) switch
    {
        ({ Alive: false }, _) => "dead",
        ({ Stunned: true }, _) => "stunned",
        ({ Class: not PetClass.Tank }, FighterAction.Cover) => "bad_action",
        ({ Class: not PetClass.Fighter }, FighterAction.Power) => "bad_action",
        ({ Class: not PetClass.Mage }, FighterAction.Fire or FighterAction.Heal) => "bad_action",
        ({ DodgedLast: true }, FighterAction.Dodge) => "dodge_twice",
        ({ Mana: < FireMana }, FighterAction.Fire) => "no_mana",
        ({ Mana: < HealMana }, FighterAction.Heal) => "no_mana",
        _ => null,
    };

    /// <summary>Медаль: 🥇 выжило не меньше ⅔, 🥈 не меньше ⅓, иначе 🥉.</summary>
    public static Medal MedalFor(int alive, int total) =>
        alive * 3 >= total * 2 ? Medal.Gold : alive * 3 >= total ? Medal.Silver : Medal.Bronze;

    /// <summary>
    /// Разыгрывает ход: лечение и заслоны → удары игроков → (если босс жив) ход босса → выбывшие.
    /// Каждый шаг — событие ленты для анимации.
    /// </summary>
    public static TurnResult Resolve(
        BossKind boss,
        BossIntent intent,
        IReadOnlyList<Fighter> fighters,
        IReadOnlyDictionary<Guid, FighterChoice> choices,
        int bossHp,
        int bossMax,
        int turn,
        bool doubled,
        int cheers)
    {
        var team = fighters.ToDictionary(f => f.UserId);
        var feed = new List<Fx>();
        var acts = new Dictionary<Guid, FighterAction?>();

        #region Выбор и тыква

        foreach (var f in fighters.Where(f => f.Alive))
        {
            if (f.Stunned)
            {
                acts[f.UserId] = null;
                continue;
            }

            // Не выбрал или выбрал недоступное — слабая атака.
            acts[f.UserId] = choices.TryGetValue(f.UserId, out var c) && Unavailable(f, c.Action) is null ? c.Action : null;
        }

        #endregion

        #region Лечение и заслоны

        foreach (var (id, action) in acts)
        {
            if (action == FighterAction.Heal && choices[id].TargetId is { } targetId && team.TryGetValue(targetId, out var target) && target.Alive)
            {
                var healer = team[id];
                var amount = Math.Min(target.MaxHp - target.Hp, 2 * healer.Intellect);
                team[id] = healer with { Mana = healer.Mana - HealMana };
                team[targetId] = team[targetId] with { Hp = team[targetId].Hp + amount };
                feed.Add(new Fx(FxKind.Heal, id, targetId, amount));
            }
            else if (action == FighterAction.Cover)
            {
                feed.Add(new Fx(FxKind.Cover, id, null, 0));
            }
        }

        #endregion

        #region Удары игроков

        var bonus = Math.Min(MaxCheerBonus, cheers);
        var shield = intent.Move == BossMoveKind.Shield;
        if (shield)
        {
            feed.Add(new Fx(FxKind.BossShield, null, null, 0));
        }

        foreach (var (id, action) in acts)
        {
            var f = team[id];
            if (fighters.First(x => x.UserId == id).Stunned)
            {
                feed.Add(new Fx(FxKind.Stunned, id, null, 0));
                continue;
            }

            var (kind, raw) = action switch
            {
                FighterAction.Attack => (FxKind.Attack, f.Damage + bonus),
                FighterAction.Power => (FxKind.Power, 2 * (f.Damage + bonus)),
                FighterAction.Fire => (FxKind.Fire, 3 * f.Intellect + bonus),
                null => (FxKind.Weak, (f.Damage + bonus + 1) / 2),
                _ => (FxKind.Attack, -1),
            };
            if (raw < 0)
            {
                // Уклон, заслон и лечение не бьют.
                continue;
            }

            if (action == FighterAction.Fire)
            {
                team[id] = f with { Mana = f.Mana - FireMana };
            }

            var dealt = shield ? (raw + 1) / 2 : raw;
            bossHp -= dealt;
            feed.Add(new Fx(kind, id, null, dealt));
        }

        #endregion

        if (bossHp <= 0)
        {
            return new TurnResult(Finish(team, acts, fighters), 0, false, feed);
        }

        #region Ход босса

        var def = Bosses[boss].Moves.First(m => m.Kind == intent.Move);
        var nextDoubled = doubled;
        if (def.SelfHeal > 0)
        {
            var healed = Math.Min(bossMax - bossHp, (int)Math.Ceiling(bossMax * def.SelfHeal));
            bossHp += healed;
            feed.Add(new Fx(FxKind.BossHeal, null, null, healed));
        }
        else if (def.Windup)
        {
            nextDoubled = true;
            feed.Add(new Fx(FxKind.BossWindup, null, null, 0));
        }
        else if (def.Multiplier > 0)
        {
            nextDoubled = false;
            var hit = Hit(boss, intent.Move, fighters, turn, bossHp, bossMax, doubled);
            var targets = intent.Targets.Where(t => team.TryGetValue(t, out var f) && f.Alive).ToList();
            var tanks = acts.Where(a => a.Value == FighterAction.Cover).Select(a => a.Key).ToList();
            if (tanks.Count > 0 && targets.Count > 0)
            {
                // Заслон: весь урон по целям — танкам, вдвое меньше, поровну.
                var share = (int)Math.Ceiling(hit * targets.Count / 2.0m / tanks.Count);
                foreach (var tank in tanks)
                {
                    team[tank] = team[tank] with { Hp = team[tank].Hp - share };
                    feed.Add(new Fx(FxKind.Covered, tank, null, share));
                }
            }
            else
            {
                foreach (var t in targets)
                {
                    if (acts[t] == FighterAction.Dodge)
                    {
                        feed.Add(new Fx(FxKind.Dodged, null, t, 0));
                        continue;
                    }

                    team[t] = team[t] with { Hp = team[t].Hp - hit, Stunned = def.Stun };
                    feed.Add(new Fx(FxKind.BossHit, null, t, hit));
                }
            }
        }

        #endregion

        foreach (var f in fighters.Where(f => f.Alive && !team[f.UserId].Alive))
        {
            feed.Add(new Fx(FxKind.Knockout, null, f.UserId, 0));
        }

        return new TurnResult(Finish(team, acts, fighters), bossHp, nextDoubled, feed);
    }

    /// <summary>Конец хода: мана +1, выбывшим здоровье 0, кто уклонялся, тыква отсидела.</summary>
    private static List<Fighter> Finish(Dictionary<Guid, Fighter> team, Dictionary<Guid, FighterAction?> acts, IReadOnlyList<Fighter> before) =>
        before.Select(b =>
        {
            var f = team[b.UserId];
            if (!f.Alive)
            {
                return f with { Hp = 0, Stunned = false, DodgedLast = false };
            }

            // Тыква, которую наложили в этот ход, остаётся на следующий; отсиженная — снимается.
            var stunned = f.Stunned && !b.Stunned;
            return f with
            {
                Mana = Math.Min(f.MaxMana, f.Mana + 1),
                DodgedLast = acts.GetValueOrDefault(b.UserId) == FighterAction.Dodge,
                Stunned = stunned,
            };
        }).ToList();

    private enum Target
    {
        None,
        One,
        Two,
        All,
    }

    private record BossMove(BossMoveKind Kind, int Weight, decimal Multiplier, Target Target, decimal SelfHeal = 0, bool Windup = false, bool Stun = false);

    private record BossDef(int HpPerAttack, decimal Strength, IReadOnlyList<BossMove> Moves);
}
