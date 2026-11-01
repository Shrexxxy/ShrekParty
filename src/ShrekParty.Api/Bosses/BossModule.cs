using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Pets;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Bosses;

/// <summary>Фаза боя.</summary>
public enum BossPhase
{
    /// <summary>Босс появляется.</summary>
    Intro,

    /// <summary>Босс объявил ход, игроки выбирают действия.</summary>
    Choosing,

    /// <summary>Экраны проигрывают ленту хода.</summary>
    Showing,

    /// <summary>Босс побеждён.</summary>
    Won,

    /// <summary>Выбыли все.</summary>
    Lost,
}

/// <summary>Настройки ивента: какой босс.</summary>
public record BossSettings(BossKind? Boss = null);

/// <summary>Состояние боя в Event.StateJson.</summary>
/// <param name="Step">Номер перехода: «Дальше» ведущего с устаревшим номером отклоняется.</param>
/// <param name="NextDoubled">Босс замахнулся — следующий удар двойной.</param>
/// <param name="Cheers">Кто из выбывших поддержал в этот ход.</param>
/// <param name="CheerBonus">Прибавка к ударам в этот ход — от поддержки в прошлый.</param>
/// <param name="Feed">Лента последнего разыгранного хода.</param>
/// <param name="Names">Логины и клички бойцов на момент старта — экранам без запросов к базе.</param>
public record BossState(
    BossKind Boss,
    BossPhase Phase,
    int Turn,
    int Step,
    DateTimeOffset? Deadline,
    int BossHp,
    int BossMax,
    bool NextDoubled,
    BossIntent? Intent,
    IReadOnlyList<Fighter> Fighters,
    IReadOnlyDictionary<Guid, FighterChoice> Choices,
    IReadOnlyList<Guid> Cheers,
    int CheerBonus,
    IReadOnlyList<Fx> Feed,
    Medal? Medal,
    IReadOnlyDictionary<Guid, BossName> Names);

/// <summary>Логин хозяина и кличка драконоослика.</summary>
public record BossName(string Login, string Name);

/// <summary>Боец для экранов; выбор виден всем — чтобы договариваться вслух.</summary>
public record BossFighterDto(
    Guid UserId, string Login, string Name, PetClass Class, int Hp, int MaxHp, int Mana, int MaxMana, bool Alive, bool Stunned, FighterAction? Choice);

/// <summary>Действие и почему его сейчас нельзя (null — можно).</summary>
public record BossOptionDto(FighterAction Action, string? Unavailable);

/// <summary>Детали боя.</summary>
/// <param name="Rage">Множитель ярости босса сейчас (1 — без ярости).</param>
/// <param name="MyOptions">Действия зрителя-участника; остальным — пусто.</param>
/// <param name="Survivors">Сколько питомцев ещё в бою.</param>
public record BossDetails(
    BossPhase Phase,
    int Turn,
    int Step,
    DateTimeOffset? Deadline,
    BossKind Boss,
    int BossHp,
    int BossMax,
    decimal Rage,
    BossIntent? Intent,
    IReadOnlyList<BossFighterDto> Fighters,
    Guid? MyId,
    FighterChoice? MyChoice,
    IReadOnlyList<BossOptionDto> MyOptions,
    IReadOnlyList<Fx> Feed,
    int CheerBonus,
    bool ICheered,
    Medal? Medal,
    int Survivors);

/// <summary>
/// Бой с боссом: появление → [выбор → разыгрывание] × N → победа или поражение. Ходов без ограничения, после 8-го — ярость.
/// </summary>
public class BossModule(AppDbContext db, ILiveNotifier live, PetService pets, Random random) : IEventModule
{
    /// <summary>Появление босса.</summary>
    public static readonly TimeSpan IntroTime = TimeSpan.FromSeconds(8);

    /// <summary>Выбор действий.</summary>
    public static readonly TimeSpan ChoosingTime = TimeSpan.FromSeconds(20);

    /// <summary>Одно событие ленты на экране.</summary>
    public static readonly TimeSpan FxTime = TimeSpan.FromSeconds(0.6);

    /// <summary>Пауза после ленты и минимальная длина показа.</summary>
    public static readonly TimeSpan ShowingTail = TimeSpan.FromSeconds(1);

    /// <summary>Разыгрывание хода не короче.</summary>
    public static readonly TimeSpan ShowingMin = TimeSpan.FromSeconds(3);

    /// <summary>Одна блокировка на действия, «Дальше» и тик.</summary>
    internal static readonly SemaphoreSlim Lock = new(1, 1);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Настройки с формы приходят с именем босса строкой.</summary>
    private static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public EventType Type => EventType.BossFight;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        var settings = ev.SettingsJson is null ? null : JsonSerializer.Deserialize<BossSettings>(ev.SettingsJson, SettingsJson);
        if (settings?.Boss is not { } boss)
        {
            throw new BadInputException("bad_boss");
        }

        // Кто так и не натапал яйцо — вылупляется сам: в бой идут все.
        await pets.HatchAllAsync();
        var all = await pets.AllAsync();
        var fighters = all
            .Select(p => new Fighter(p.UserId, p.Class, p.Health, p.Health, p.Mana, p.Mana, p.Damage, p.Intellect, false, false))
            .ToList();
        if (fighters.Count == 0)
        {
            throw new EventRuleException("no_pets");
        }

        var max = BossRules.MaxHp(boss, fighters);
        SetState(ev, new BossState(
            boss, BossPhase.Intro, 0, 0, ev.StartedAt!.Value + IntroTime, max, max, false, null, fighters,
            new Dictionary<Guid, FighterChoice>(), [], 0, [], null, all.ToDictionary(p => p.UserId, p => new BossName(p.Login, p.Name))));
    }

    /// <inheritdoc/>
    public Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var names = state.Names;
        var me = state.Fighters.FirstOrDefault(f => f.UserId == viewerId);
        var showChoices = state.Phase == BossPhase.Choosing;

        return Task.FromResult<object?>(new BossDetails(
            state.Phase,
            state.Turn,
            state.Step,
            state.Deadline,
            state.Boss,
            state.BossHp,
            state.BossMax,
            RageOf(state),
            state.Phase == BossPhase.Choosing ? state.Intent : null,
            state.Fighters
                .Where(f => names.ContainsKey(f.UserId))
                .Select(f => new BossFighterDto(
                    f.UserId, names[f.UserId].Login, names[f.UserId].Name, f.Class, f.Hp, f.MaxHp, f.Mana, f.MaxMana, f.Alive, f.Stunned,
                    showChoices && state.Choices.TryGetValue(f.UserId, out var c) ? c.Action : null))
                .ToList(),
            me?.UserId,
            me is not null && state.Choices.TryGetValue(me.UserId, out var mine) ? mine : null,
            me is null ? [] : OptionsFor(me).Select(a => new BossOptionDto(a, BossRules.Unavailable(me, a))).ToList(),
            state.Phase is BossPhase.Showing or BossPhase.Won or BossPhase.Lost ? state.Feed : [],
            state.CheerBonus,
            me is not null && state.Cheers.Contains(me.UserId),
            state.Medal,
            state.Fighters.Count(f => f.Alive)));
    }

    /// <inheritdoc/>
    public async Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        await Lock.WaitAsync();
        try
        {
            // Действие могло прийти, пока движок читал ивент.
            await db.Entry(ev).ReloadAsync();
            var state = StateOf(ev);
            if (ev.Status != EventStatus.Active || state.Deadline is not { } deadline || now < deadline)
            {
                return false;
            }

            SetState(ev, Advance(state, now, random));
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
    /// <remarks>Мест нет: награда и штраф — отдельно (аукцион и штраф за поражение).</remarks>
    public Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev) => Task.FromResult<IReadOnlyList<Ranked<Guid>>>([]);

    internal static BossState StateOf(Event ev) => JsonSerializer.Deserialize<BossState>(ev.StateJson!, Json)!;

    internal static void SetState(Event ev, BossState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);

    /// <summary>Действия, которые вообще есть у класса.</summary>
    internal static IEnumerable<FighterAction> OptionsFor(Fighter fighter) =>
        new[] { FighterAction.Attack, FighterAction.Dodge }.Concat(fighter.Class switch
        {
            PetClass.Tank => [FighterAction.Cover],
            PetClass.Fighter => [FighterAction.Power],
            _ => new[] { FighterAction.Fire, FighterAction.Heal },
        });

    /// <summary>Все живые выбрали — ход можно разыгрывать.</summary>
    internal static bool AllChose(BossState state) =>
        state.Fighters.Where(f => f.Alive && !f.Stunned).All(f => state.Choices.ContainsKey(f.UserId));

    /// <summary>
    /// Следующая фаза — по таймеру, по «Дальше» ведущего или когда все выбрали.
    /// </summary>
    internal static BossState Advance(BossState state, DateTimeOffset now, Random random) => Next(state, now, random) with { Step = state.Step + 1 };

    private static BossState Next(BossState state, DateTimeOffset now, Random random) => state.Phase switch
    {
        BossPhase.Intro => NewTurn(state, now, random),
        BossPhase.Choosing => Resolve(state, now),
        BossPhase.Showing when state.BossHp <= 0 => state with
        {
            Phase = BossPhase.Won,
            Deadline = null,
            Medal = BossRules.MedalFor(state.Fighters.Count(f => f.Alive), state.Fighters.Count),
        },
        BossPhase.Showing when state.Fighters.All(f => !f.Alive) => state with { Phase = BossPhase.Lost, Deadline = null },
        BossPhase.Showing => NewTurn(state, now, random),
        _ => state,
    };

    private static BossState NewTurn(BossState state, DateTimeOffset now, Random random) => state with
    {
        Phase = BossPhase.Choosing,
        Turn = state.Turn + 1,
        Deadline = now + ChoosingTime,
        Intent = BossRules.Plan(state.Boss, state.Fighters, random),
        Choices = new Dictionary<Guid, FighterChoice>(),
        Feed = [],
    };

    private static BossState Resolve(BossState state, DateTimeOffset now)
    {
        var result = BossRules.Resolve(
            state.Boss, state.Intent!, state.Fighters, state.Choices, state.BossHp, state.BossMax, state.Turn, state.NextDoubled, state.CheerBonus);
        var playback = FxTime * result.Feed.Count + ShowingTail;
        return state with
        {
            Phase = BossPhase.Showing,
            Deadline = now + (playback < ShowingMin ? ShowingMin : playback),
            BossHp = result.BossHp,
            NextDoubled = result.NextDoubled,
            Fighters = result.Fighters,
            Feed = result.Feed,
            // Поддержка этого хода усиливает следующий.
            CheerBonus = state.Cheers.Count,
            Cheers = [],
        };
    }

    private static decimal RageOf(BossState state)
    {
        var rage = 1 + 0.15m * Math.Max(0, state.Turn - (BossRules.RageFromTurn - 1));
        return state.Boss == BossKind.Fairy && state.BossHp * 3 < state.BossMax ? rage * 1.5m : rage;
    }
}
