using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
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

/// <summary>Настройки ивента: какой босс и сколько секунд на выбор (у Крестьянина таймера нет).</summary>
public record BossSettings(BossKind? Boss = null, int? ChooseSeconds = null);

/// <summary>Состояние боя в Event.StateJson.</summary>
/// <param name="Step">Номер перехода: «Дальше» ведущего с устаревшим номером отклоняется.</param>
/// <param name="NextDoubled">Босс замахнулся — следующий удар двойной.</param>
/// <param name="Cheers">Кто из выбывших поддержал в этот ход.</param>
/// <param name="CheerBonus">Прибавка к ударам в этот ход — от поддержки в прошлый.</param>
/// <param name="Feed">Лента последнего разыгранного хода.</param>
/// <param name="Names">Логины и клички бойцов на момент старта — экранам без запросов к базе.</param>
/// <param name="ChooseSeconds">Время на выбор; null — ждём, пока все подтвердят.</param>
/// <param name="Confirmed">Кто подтвердил выбор в этот ход.</param>
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
    IReadOnlyDictionary<Guid, BossName> Names,
    int? ChooseSeconds = null,
    IReadOnlyList<Guid>? Confirmed = null);

/// <summary>Логин хозяина, кличка драконоослика, его рисунок и уровень на старте боя — для подписи на экранах.</summary>
public record BossName(string Login, string Name, int Variant = 1, int Level = 1);

/// <summary>Боец для экранов; выбор виден всем — чтобы договариваться вслух.</summary>
public record BossFighterDto(
    Guid UserId, string Login, string? AvatarUrl, string Name, PetClass Class, int Variant, int Level, int Hp, int MaxHp, int Mana, int MaxMana, bool Alive, bool Stunned, FighterAction? Choice, bool Confirmed);

/// <summary>Приз босса на экране итога.</summary>
/// <param name="Open">Медаль боя его открыла.</param>
public record BossPrizeDto(string Name, Medal Medal, bool Open, bool Sold);

/// <summary>Ход босса, объявленный заранее; Damage — примерный урон по каждой цели (null — ход не бьёт).</summary>
public record BossIntentDto(BossMoveKind Move, IReadOnlyList<Guid> Targets, int? Damage);

/// <summary>Действие, почему его сейчас нельзя (null — можно) и сколько урона или лечения даст — для подписи на кнопке.</summary>
public record BossOptionDto(FighterAction Action, string? Unavailable, int? Amount);

/// <summary>Детали боя.</summary>
/// <param name="Rage">Множитель ярости босса сейчас (1 — без ярости).</param>
/// <param name="PlayingMove">Ход босса, который сейчас проигрывается (стрелы, звёзды…).</param>
/// <param name="MyOptions">Действия зрителя-участника; остальным — пусто.</param>
/// <param name="Survivors">Сколько питомцев ещё в бою.</param>
/// <param name="Prizes">Призы этого босса — на экране итога.</param>
public record BossDetails(
    BossPhase Phase,
    int Turn,
    int Step,
    DateTimeOffset? Deadline,
    BossKind Boss,
    int BossHp,
    int BossMax,
    decimal Rage,
    BossIntentDto? Intent,
    BossMoveKind? PlayingMove,
    IReadOnlyList<BossFighterDto> Fighters,
    Guid? MyId,
    FighterChoice? MyChoice,
    IReadOnlyList<BossOptionDto> MyOptions,
    IReadOnlyList<Fx> Feed,
    int CheerBonus,
    bool ICheered,
    bool MyConfirmed,
    Medal? Medal,
    int Survivors,
    IReadOnlyList<BossPrizeDto> Prizes);

/// <summary>
/// Бой с боссом: появление → [выбор → разыгрывание] × N → победа или поражение. Ходов без ограничения, после 8-го — ярость.
/// </summary>
public class BossModule(AppDbContext db, ILiveNotifier live, PetService pets, Random random) : IEventModule
{
    /// <summary>Штраф каждому участнику за поражение (не больше осликов на руках).</summary>
    public const int LossPenalty = 25;

    /// <summary>Появление босса.</summary>
    public static readonly TimeSpan IntroTime = TimeSpan.FromSeconds(8);

    /// <summary>Время на выбор по умолчанию — у всех, кроме Крестьянина (у него ждём, пока все подтвердят).</summary>
    public const int DefaultChooseSeconds = 60;

    /// <summary>Одно событие ленты на экране — медленно, чтобы успеть понять, кто что сделал.</summary>
    public static readonly TimeSpan FxTime = TimeSpan.FromSeconds(1.5);

    /// <summary>Пауза после ленты.</summary>
    public static readonly TimeSpan ShowingTail = TimeSpan.FromSeconds(2);

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

    // Призы одинаковы для всех зрителей рассылки — читаем один раз на область (снапшот собирается в одной).
    private IReadOnlyList<BossPrizeDto>? _prizes;

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
            new Dictionary<Guid, FighterChoice>(), [], 0, [], null, all.ToDictionary(p => p.UserId, p => new BossName(p.Login, p.Name, p.Variant, p.Level)),
            boss == BossKind.Peasant ? null : settings.ChooseSeconds ?? DefaultChooseSeconds));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var prizes = state.Phase is BossPhase.Won or BossPhase.Lost ? _prizes ??= await PrizesAsync(state) : [];
        var names = state.Names;
        // Аватарку можно сменить посреди вечера — берём свежую, а не со старта боя.
        var ids = state.Fighters.Select(f => f.UserId).ToList();
        var avatars = (await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync()).ToDictionary(u => u.Id, u => u.ToMe().AvatarUrl);
        var me = state.Fighters.FirstOrDefault(f => f.UserId == viewerId);
        var showChoices = state.Phase == BossPhase.Choosing;

        return new BossDetails(
            state.Phase,
            state.Turn,
            state.Step,
            state.Deadline,
            state.Boss,
            state.BossHp,
            state.BossMax,
            RageOf(state),
            state.Phase == BossPhase.Choosing && state.Intent is { } intent ? IntentOf(state, intent) : null,
            state.Phase == BossPhase.Showing ? state.Intent?.Move : null,
            state.Fighters
                .Where(f => names.ContainsKey(f.UserId))
                .Select(f => new BossFighterDto(
                    f.UserId, names[f.UserId].Login, avatars.GetValueOrDefault(f.UserId), names[f.UserId].Name, f.Class,
                    names[f.UserId].Variant, names[f.UserId].Level, f.Hp, f.MaxHp, f.Mana, f.MaxMana, f.Alive, f.Stunned,
                    showChoices && state.Choices.TryGetValue(f.UserId, out var c) ? c.Action : null,
                    ConfirmedOf(state).Contains(f.UserId)))
                .ToList(),
            me?.UserId,
            me is not null && state.Choices.TryGetValue(me.UserId, out var mine) ? mine : null,
            me is null ? [] : OptionsFor(me).Select(a => new BossOptionDto(a, BossRules.Unavailable(me, a), BossRules.Amount(me, a, state.CheerBonus))).ToList(),
            state.Phase is BossPhase.Showing or BossPhase.Won or BossPhase.Lost ? state.Feed : [],
            state.CheerBonus,
            me is not null && state.Cheers.Contains(me.UserId),
            me is not null && ConfirmedOf(state).Contains(me.UserId),
            state.Medal,
            state.Fighters.Count(f => f.Alive),
            prizes);
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

    /// <inheritdoc/>
    /// <remarks>Поражение — минус 25 каждому участнику, но не больше, чем у него на руках.</remarks>
    public async Task AfterFinishAsync(Event ev)
    {
        // Движок прочитал ивент до завершения — ход мог успеть после; берём последнее состояние под блокировкой.
        await Lock.WaitAsync();
        try
        {
            await db.Entry(ev).ReloadAsync();
        }
        finally
        {
            Lock.Release();
        }

        var state = StateOf(ev);
        if (state.Phase != BossPhase.Lost)
        {
            return;
        }

        var ids = state.Fighters.Select(f => f.UserId).ToList();
        var awarded = await db.DonkeyAwards.Where(a => ids.Contains(a.UserId)).GroupBy(a => a.UserId)
            .Select(g => new { g.Key, Sum = g.Sum(a => a.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        var spent = await db.AuctionSales.Where(s => ids.Contains(s.UserId)).GroupBy(s => s.UserId)
            .Select(g => new { g.Key, Sum = g.Sum(s => s.Price) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        foreach (var id in ids)
        {
            var penalty = Math.Min(LossPenalty, awarded.GetValueOrDefault(id) - spent.GetValueOrDefault(id));
            if (penalty > 0)
            {
                db.DonkeyAwards.Add(new DonkeyAward
                {
                    Id = Guid.NewGuid(), UserId = id, EventId = ev.Id, Amount = -penalty, Reason = $"{ev.Title}: поражение", CreatedAt = ev.FinishedAt ?? DateTimeOffset.UtcNow,
                });
            }
        }
    }

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

    /// <summary>Все живые и не оглушённые подтвердили — ход можно разыгрывать.</summary>
    internal static bool AllConfirmed(BossState state) =>
        state.Fighters.Where(f => f.Alive && !f.Stunned).All(f => ConfirmedOf(state).Contains(f.UserId));

    /// <summary>Подтвердившие (в старом состоянии поля ещё нет).</summary>
    internal static IReadOnlyList<Guid> ConfirmedOf(BossState state) => state.Confirmed ?? [];

    /// <summary>Объявленный ход с примерным уроном по каждой цели — чтобы игроки понимали, что сейчас будет.</summary>
    private static BossIntentDto IntentOf(BossState state, BossIntent intent)
    {
        var damage = BossRules.Hit(state.Boss, intent.Move, state.Fighters, state.Turn, state.BossHp, state.BossMax, state.NextDoubled);
        return new BossIntentDto(intent.Move, intent.Targets, damage > 0 ? damage : null);
    }

    /// <summary>
    /// Следующая фаза — по таймеру, по «Дальше» ведущего или когда все выбрали.
    /// </summary>
    internal static BossState Advance(BossState state, DateTimeOffset now, Random random) => Next(state, now, random) with { Step = state.Step + 1 };

    private async Task<IReadOnlyList<BossPrizeDto>> PrizesAsync(BossState state)
    {
        var opened = state.Medal is { } medal ? BossRules.Opened(medal) : [];
        // Медаль в базе — строкой: сортируем уже в памяти, по значению, а не по алфавиту.
        var lots = (await db.AuctionLots.AsNoTracking().Where(l => l.Boss == state.Boss && l.Medal != null).ToListAsync()).OrderBy(l => l.Medal).ToList();
        var sold = await db.AuctionSales.Select(s => s.LotId).ToListAsync();
        return lots.Select(l => new BossPrizeDto(l.Name, l.Medal!.Value, opened.Contains(l.Medal!.Value), sold.Contains(l.Id))).ToList();
    }

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
        Deadline = state.ChooseSeconds is { } seconds ? now + TimeSpan.FromSeconds(seconds) : null,
        Intent = BossRules.Plan(state.Boss, state.Fighters, random),
        Choices = new Dictionary<Guid, FighterChoice>(),
        Confirmed = [],
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
