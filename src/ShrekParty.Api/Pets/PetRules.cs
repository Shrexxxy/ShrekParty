namespace ShrekParty.Api.Pets;

/// <summary>Класс драконоослика — по ведущей характеристике.</summary>
public enum PetClass
{
    /// <summary>Танк: сила, много здоровья.</summary>
    Tank,

    /// <summary>Боец: ловкость, основной урон.</summary>
    Fighter,

    /// <summary>Маг: интеллект, мана.</summary>
    Mage,
}

/// <summary>Характеристика питомца.</summary>
public enum PetStat
{
    /// <summary>Сила — здоровье.</summary>
    Strength,

    /// <summary>Ловкость — урон.</summary>
    Agility,

    /// <summary>Интеллект — мана.</summary>
    Intellect,
}

/// <summary>Характеристики и что из них следует в бою.</summary>
public record PetStats(int Strength, int Agility, int Intellect)
{
    /// <summary>Здоровье: 20 + 5 × сила.</summary>
    public int Health => 20 + 5 * Strength;

    /// <summary>Урон атаки: 2 + ловкость.</summary>
    public int Damage => 2 + Agility;

    /// <summary>Мана: 2 × интеллект.</summary>
    public int Mana => 2 * Intellect;
}

/// <summary>С чем питомец вылупился.</summary>
/// <param name="FirstMinor">Какая из неведущих получает очко первой — дальше по очереди.</param>
public record PetStart(PetClass Class, PetStats Stats, PetStat FirstMinor);

/// <summary>
/// Правила драконоосликов: раздача классов, стартовые характеристики, уровень от заработанных осликов, прокачка.
/// </summary>
public static class PetRules
{
    /// <summary>Заработанных осликов на уровень.</summary>
    public const int XpPerLevel = 50;

    /// <summary>Тапов, чтобы вылупился.</summary>
    public const int EggTaps = 15;

    private const int LeadStart = 6;
    private const int StartSum = 12;

    private static readonly int[] MinorSplits = [2, 3, 4];

    /// <summary>Ведущая характеристика класса.</summary>
    public static PetStat Lead(PetClass petClass) => petClass switch
    {
        PetClass.Tank => PetStat.Strength,
        PetClass.Fighter => PetStat.Agility,
        _ => PetStat.Intellect,
    };

    /// <summary>
    /// Класс для нового питомца: которого сейчас меньше всего, при ничьей — случайный. Так компания делится поровну.
    /// </summary>
    public static PetClass PickClass(IReadOnlyDictionary<PetClass, int> counts, Random random)
    {
        var least = Enum.GetValues<PetClass>().Min(c => counts.GetValueOrDefault(c));
        var candidates = Enum.GetValues<PetClass>().Where(c => counts.GetValueOrDefault(c) == least).ToArray();
        return candidates[random.Next(candidates.Length)];
    }

    /// <summary>
    /// Стартовые характеристики: ведущая 6, две другие делят 6 как 2+4, 3+3 или 4+2 — ведущая всегда строго больше.
    /// </summary>
    public static PetStart Roll(PetClass petClass, Random random)
    {
        var lead = Lead(petClass);
        var (first, second) = Minors(lead);
        var a = MinorSplits[random.Next(MinorSplits.Length)];
        var values = new Dictionary<PetStat, int> { [lead] = LeadStart, [first] = a, [second] = StartSum - LeadStart - a };

        // Первое очко прокачки — отстающей неведущей.
        var firstMinor = values[first] < values[second] ? first
            : values[second] < values[first] ? second
            : random.Next(2) == 0 ? first : second;
        return new PetStart(petClass, ToStats(values), firstMinor);
    }

    /// <summary>Уровень: 1 + заработанные / 50.</summary>
    public static int Level(int earned) => 1 + Math.Max(0, earned) / XpPerLevel;

    /// <summary>Сколько всего надо заработать, чтобы получить следующий уровень.</summary>
    public static int NextLevelAt(int level) => level * XpPerLevel;

    /// <summary>
    /// Характеристики на уровне: каждый уровень +2 ведущей и +1 неведущей по очереди, начиная с <see cref="PetStart.FirstMinor"/>.
    /// </summary>
    public static PetStats StatsAt(PetStart start, int level)
    {
        var ups = Math.Max(0, level - 1);
        var lead = Lead(start.Class);
        var (a, b) = Minors(lead);
        var other = start.FirstMinor == a ? b : a;
        var values = new Dictionary<PetStat, int>
        {
            [PetStat.Strength] = start.Stats.Strength,
            [PetStat.Agility] = start.Stats.Agility,
            [PetStat.Intellect] = start.Stats.Intellect,
        };
        values[lead] += 2 * ups;
        values[start.FirstMinor] += (ups + 1) / 2;
        values[other] += ups / 2;
        return ToStats(values);
    }

    private static (PetStat First, PetStat Second) Minors(PetStat lead)
    {
        var minors = Enum.GetValues<PetStat>().Where(s => s != lead).ToArray();
        return (minors[0], minors[1]);
    }

    private static PetStats ToStats(Dictionary<PetStat, int> v) => new(v[PetStat.Strength], v[PetStat.Agility], v[PetStat.Intellect]);
}
