using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Mafia;

/// <summary>Роль в мафии.</summary>
public enum MafiaRole
{
    Mafia,
    Doctor,
    Commissar,
    Civilian,
}

/// <summary>Фаза мафии.</summary>
public enum MafiaPhase
{
    /// <summary>Первая ночь: знакомство, без убийства.</summary>
    Intro,

    /// <summary>Ночь: мафия, доктор, комиссар (мирные — заглушка).</summary>
    Night,

    /// <summary>Итог ночи.</summary>
    Morning,

    /// <summary>Речи по кругу.</summary>
    Speech,

    /// <summary>Одновременное голосование.</summary>
    Voting,

    /// <summary>Голоса раскрыты.</summary>
    VoteResult,

    /// <summary>Последнее слово выбывшего.</summary>
    LastWord,

    /// <summary>Игра окончена.</summary>
    Ended,
}

/// <summary>Победившая команда.</summary>
public enum MafiaTeam
{
    Civilians,
    Mafia,
}

/// <summary>За что ослики в мафии (кроме победы).</summary>
public enum MafiaBonusKind
{
    /// <summary>Ночные задания мирного.</summary>
    Tasks,

    /// <summary>Убийство — каждому живому мафиози.</summary>
    Kill,

    /// <summary>Комиссар нашёл мафию (каждую — один раз).</summary>
    Check,

    /// <summary>Доктор спас жертву мафии.</summary>
    Save,

    /// <summary>Посадили мафию — каждому живому не-мафии.</summary>
    Oust,
}

/// <summary>Бонус в копилке партии; начисляется в конце игры.</summary>
/// <param name="Count">Сколько раз (заданий, убийств…).</param>
public record MafiaBonus(Guid UserId, MafiaBonusKind Kind, int Count, int Amount);

/// <summary>Игрок за столом.</summary>
public record MafiaSeat(Guid UserId, MafiaRole Role, bool Alive);

/// <summary>Проверка комиссара.</summary>
public record MafiaCheck(int Day, Guid TargetId, bool IsMafia);

/// <summary>Состояние мафии в Event.StateJson — целиком, без отдельных таблиц.</summary>
/// <param name="NightActions">Ходы этой ночи: кто → на кого.</param>
/// <param name="LastHeal">Кого доктор лечил прошлой ночью — второй раз подряд нельзя.</param>
/// <param name="Revealed">Чьи роли открыты всем (выбывшие голосованием).</param>
/// <param name="Step">Номер перехода: «Дальше» ведущего с устаревшим номером отклоняется — таймер успел раньше.</param>
/// <param name="Tasks">Сколько заданий сделано этой ночью — у всех, роли делают их для вида.</param>
/// <param name="TaskAt">Когда сделано последнее задание — защита от двойного тапа.</param>
/// <param name="Bonuses">Копилка партии.</param>
public record MafiaState(
    MafiaPhase Phase,
    int Day,
    DateTimeOffset? Deadline,
    IReadOnlyList<MafiaSeat> Seats,
    IReadOnlyDictionary<Guid, Guid> NightActions,
    Guid? LastHeal,
    IReadOnlyList<MafiaCheck> Checks,
    Guid? NightVictim,
    IReadOnlyList<Guid> SpeechQueue,
    int SpeakerIndex,
    IReadOnlyDictionary<Guid, Guid?> Votes,
    Guid? Eliminated,
    IReadOnlyList<Guid> Revealed,
    MafiaTeam? Winner,
    int Step,
    IReadOnlyDictionary<Guid, int> Tasks,
    IReadOnlyDictionary<Guid, DateTimeOffset> TaskAt,
    IReadOnlyList<MafiaBonus> Bonuses);

/// <summary>Игрок в деталях; роль — только тем, кому положено её видеть.</summary>
public record MafiaPlayerDto(Guid UserId, string Login, string? AvatarUrl, bool Alive, MafiaRole? Role);

/// <summary>Ход ночи (кто → на кого).</summary>
public record MafiaActionDto(Guid UserId, Guid TargetId);

/// <summary>Голос; null — воздержался.</summary>
public record MafiaVoteDto(Guid VoterId, Guid? TargetId);

/// <summary>Сколько заданий этой ночью сделал игрок (ведущему).</summary>
public record MafiaTaskDto(Guid UserId, int Done);

/// <summary>Выбывший голосованием и его роль.</summary>
public record MafiaEliminatedDto(Guid UserId, MafiaRole Role);

/// <summary>Детали мафии.</summary>
/// <param name="NightActions">Ведущему — все ходы ночи, мафии — ходы напарников, остальным — пусто.</param>
/// <param name="Checks">Комиссару — его проверки, ведущему — все.</param>
/// <param name="Votes">Голоса — после голосования всем, ведущему — сразу.</param>
/// <param name="TasksTotal">Всего заданий этой ночью — для ТВ, по ней роль не вычислить.</param>
/// <param name="Tasks">Задания по игрокам — только ведущему.</param>
/// <param name="Bonuses">Копилка по игрокам и видам: игроку — своя, ведущему и в конце — все.</param>
/// <param name="WinDonkeys">Сколько за победу каждому из команды.</param>
/// <param name="Step">Номер перехода — пульт шлёт его с «Дальше».</param>
public record MafiaDetails(
    MafiaPhase Phase,
    int Day,
    int Step,
    DateTimeOffset? Deadline,
    IReadOnlyList<MafiaPlayerDto> Players,
    MafiaRole? MyRole,
    bool IAmAlive,
    IReadOnlyList<Guid> Partners,
    Guid? MyNightTarget,
    IReadOnlyList<MafiaActionDto> NightActions,
    IReadOnlyList<MafiaCheck> Checks,
    Guid? LastHeal,
    Guid? SpeakerId,
    IReadOnlyList<Guid> SpeechQueue,
    int VotedCount,
    bool IVoted,
    IReadOnlyList<MafiaVoteDto> Votes,
    MafiaEliminatedDto? Eliminated,
    Guid? NightVictim,
    MafiaTeam? Winner,
    int MyTasksDone,
    int TasksTotal,
    IReadOnlyList<MafiaTaskDto> Tasks,
    IReadOnlyList<MafiaBonus> Bonuses,
    int WinDonkeys);

/// <summary>
/// Мафия: знакомство → утро → речи → голосование → (последнее слово) → ночь → утро → …
/// Фазы идут по таймерам, ведущий может закончить любую раньше; ночь и голосование кончаются сами, когда все сходили.
/// </summary>
public class MafiaModule(AppDbContext db, ILiveNotifier live, Random random) : IEventModule
{
    /// <summary>Меньше — не игра: 2 мафии, доктор, комиссар и хотя бы один мирный.</summary>
    public const int MinPlayers = 5;

    /// <summary>Заданий за ночь, по ослику за каждое.</summary>
    public const int MaxTasks = 5;

    /// <summary>За убийство каждому живому мафиози.</summary>
    public const int KillDonkeys = 10;

    /// <summary>Комиссару за найденную мафию.</summary>
    public const int CheckDonkeys = 10;

    /// <summary>Доктору за спасение.</summary>
    public const int SaveDonkeys = 15;

    /// <summary>Каждому живому не-мафии за посаженную мафию.</summary>
    public const int OustDonkeys = 10;

    /// <summary>Знакомство.</summary>
    public static readonly TimeSpan IntroTime = TimeSpan.FromSeconds(60);

    /// <summary>Ночь.</summary>
    public static readonly TimeSpan NightTime = TimeSpan.FromSeconds(60);

    /// <summary>Итог ночи на экране.</summary>
    public static readonly TimeSpan MorningTime = TimeSpan.FromSeconds(10);

    /// <summary>Речь одного игрока.</summary>
    public static readonly TimeSpan SpeechTime = TimeSpan.FromSeconds(60);

    /// <summary>Голосование.</summary>
    public static readonly TimeSpan VotingTime = TimeSpan.FromSeconds(60);

    /// <summary>Итог голосования на экране.</summary>
    public static readonly TimeSpan VoteResultTime = TimeSpan.FromSeconds(10);

    /// <summary>Последнее слово.</summary>
    public static readonly TimeSpan LastWordTime = TimeSpan.FromSeconds(30);

    /// <summary>Задания чаще — двойной тап, не засчитываем.</summary>
    public static readonly TimeSpan TaskInterval = TimeSpan.FromSeconds(1.5);

    /// <summary>Одна блокировка на ходы, голоса, «Дальше» и тик.</summary>
    internal static readonly SemaphoreSlim Lock = new(1, 1);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<MafiaBonusKind, string> BonusReasons = new()
    {
        [MafiaBonusKind.Tasks] = "задания",
        [MafiaBonusKind.Kill] = "убийства",
        [MafiaBonusKind.Check] = "проверки",
        [MafiaBonusKind.Save] = "спасения",
        [MafiaBonusKind.Oust] = "посадки",
    };

    /// <inheritdoc/>
    public EventType Type => EventType.Mafia;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        var players = await db.Users.Where(u => u.Role == UserRole.Player).Select(u => u.Id).ToListAsync();
        if (players.Count < MinPlayers)
        {
            throw new EventRuleException("not_enough_players");
        }

        var seats = players.ToArray();
        random.Shuffle(seats);
        var roles = new List<MafiaRole> { MafiaRole.Mafia, MafiaRole.Mafia, MafiaRole.Doctor, MafiaRole.Commissar };
        roles.AddRange(Enumerable.Repeat(MafiaRole.Civilian, seats.Length - roles.Count));
        var dealt = roles.ToArray();
        random.Shuffle(dealt);

        SetState(ev, new MafiaState(
            MafiaPhase.Intro, 0, ev.StartedAt!.Value + IntroTime,
            seats.Select((id, i) => new MafiaSeat(id, dealt[i], true)).ToList(),
            new Dictionary<Guid, Guid>(), null, [], null, [], 0, new Dictionary<Guid, Guid?>(), null, [], null,
            0, new Dictionary<Guid, int>(), new Dictionary<Guid, DateTimeOffset>(), []));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var ids = state.Seats.Select(s => s.UserId).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var me = state.Seats.FirstOrDefault(s => s.UserId == viewerId);
        var iAmMafia = me?.Role == MafiaRole.Mafia;
        var ended = state.Phase == MafiaPhase.Ended;
        var votesOpen = isAdmin || state.Phase is MafiaPhase.VoteResult or MafiaPhase.LastWord;
        var bonuses = Summary(state.Bonuses.Where(b => isAdmin || ended || b.UserId == viewerId));

        bool RoleVisible(MafiaSeat seat) =>
            isAdmin || ended || seat.UserId == viewerId || state.Revealed.Contains(seat.UserId) || iAmMafia && seat.Role == MafiaRole.Mafia;

        var partners = iAmMafia ? state.Seats.Where(s => s.Role == MafiaRole.Mafia && s.UserId != viewerId).Select(s => s.UserId).ToList() : [];
        var actions = isAdmin
            ? state.NightActions.Select(a => new MafiaActionDto(a.Key, a.Value)).ToList()
            : state.NightActions.Where(a => partners.Contains(a.Key)).Select(a => new MafiaActionDto(a.Key, a.Value)).ToList();

        return new MafiaDetails(
            state.Phase,
            state.Day,
            state.Step,
            state.Deadline,
            state.Seats
                .Where(s => users.ContainsKey(s.UserId))
                .Select(s => new MafiaPlayerDto(s.UserId, users[s.UserId].Login, users[s.UserId].ToMe().AvatarUrl, s.Alive, RoleVisible(s) ? s.Role : null))
                .ToList(),
            me?.Role,
            me?.Alive ?? false,
            partners,
            viewerId is { } v && state.NightActions.TryGetValue(v, out var target) ? target : null,
            actions,
            isAdmin ? state.Checks : me?.Role == MafiaRole.Commissar ? state.Checks : [],
            isAdmin || me?.Role == MafiaRole.Doctor ? state.LastHeal : null,
            state.Phase == MafiaPhase.Speech && state.SpeakerIndex < state.SpeechQueue.Count ? state.SpeechQueue[state.SpeakerIndex] : null,
            state.SpeechQueue,
            state.Votes.Count,
            viewerId is { } voter && state.Votes.ContainsKey(voter),
            votesOpen ? state.Votes.Select(x => new MafiaVoteDto(x.Key, x.Value)).ToList() : [],
            state.Eliminated is { } out1 && state.Phase is MafiaPhase.VoteResult or MafiaPhase.LastWord or MafiaPhase.Ended
                ? new MafiaEliminatedDto(out1, state.Seats.First(s => s.UserId == out1).Role)
                : null,
            state.Phase is MafiaPhase.Morning or MafiaPhase.Ended ? state.NightVictim : null,
            state.Winner,
            viewerId is { } doer ? state.Tasks.GetValueOrDefault(doer) : 0,
            state.Tasks.Values.Sum(),
            isAdmin ? state.Tasks.Select(t => new MafiaTaskDto(t.Key, t.Value)).ToList() : [],
            bonuses,
            ev.MaxDonkeys);
    }

    /// <inheritdoc/>
    public async Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        await Lock.WaitAsync();
        try
        {
            // Ход или голос мог прийти, пока движок читал ивент.
            await db.Entry(ev).ReloadAsync();
            var state = StateOf(ev);
            if (ev.Status != EventStatus.Active || state.Deadline is not { } deadline || now < deadline)
            {
                return false;
            }

            SetState(ev, Advance(state, now));
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
    /// <remarks>Мест нет: победу и бонусы начисляет <see cref="AfterFinishAsync"/>.</remarks>
    public Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev) => Task.FromResult<IReadOnlyList<Ranked<Guid>>>([]);

    /// <inheritdoc/>
    public async Task AfterFinishAsync(Event ev)
    {
        // Движок прочитал ивент до завершения — ход или голос мог успеть после; берём последнее состояние под блокировкой.
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
        var at = ev.FinishedAt ?? DateTimeOffset.UtcNow;
        void Add(Guid userId, int amount, string reason) => db.DonkeyAwards.Add(new DonkeyAward
        {
            Id = Guid.NewGuid(), UserId = userId, EventId = ev.Id, Amount = amount, Reason = $"{ev.Title}: {reason}", CreatedAt = at,
        });

        // Победа командная: мёртвые победители тоже получают. Игру прервали — только бонусы.
        if (state.Winner is { } winner && ev.MaxDonkeys > 0)
        {
            foreach (var seat in state.Seats.Where(s => TeamOf(s.Role) == winner))
            {
                Add(seat.UserId, ev.MaxDonkeys, "победа");
            }
        }

        foreach (var bonus in Summary(state.Bonuses))
        {
            Add(bonus.UserId, bonus.Amount, BonusReasons[bonus.Kind]);
        }
    }

    internal static MafiaState StateOf(Event ev) => JsonSerializer.Deserialize<MafiaState>(ev.StateJson!, Json)!;

    internal static void SetState(Event ev, MafiaState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);

    /// <summary>Копилка, сложенная по игроку и виду.</summary>
    internal static IReadOnlyList<MafiaBonus> Summary(IEnumerable<MafiaBonus> bonuses) =>
        bonuses.GroupBy(b => (b.UserId, b.Kind))
            .Select(g => new MafiaBonus(g.Key.UserId, g.Key.Kind, g.Sum(b => b.Count), g.Sum(b => b.Amount)))
            .ToList();

    internal static MafiaTeam TeamOf(MafiaRole role) => role == MafiaRole.Mafia ? MafiaTeam.Mafia : MafiaTeam.Civilians;

    /// <summary>
    /// Следующая фаза — по таймеру, по «Дальше» ведущего или когда все сходили.
    /// </summary>
    internal static MafiaState Advance(MafiaState state, DateTimeOffset now) => Next(state, now) with { Step = state.Step + 1 };

    /// <summary>
    /// Ночь кончится раньше, когда мафия выбрала одну жертву, доктор и комиссар сходили, а мирные сделали все задания.
    /// </summary>
    internal static bool NightComplete(MafiaState state)
    {
        var alive = state.Seats.Where(s => s.Alive).ToList();
        return MafiaTarget(state) is not null
            && alive.Where(s => s.Role is MafiaRole.Doctor or MafiaRole.Commissar).All(s => state.NightActions.ContainsKey(s.UserId))
            && alive.Where(s => s.Role == MafiaRole.Civilian).All(s => state.Tasks.GetValueOrDefault(s.UserId) >= MaxTasks);
    }

    /// <summary>
    /// Кто победил: мафий нет — мирные; мафий не меньше остальных живых — мафия.
    /// </summary>
    internal static MafiaTeam? WinnerOf(IReadOnlyList<MafiaSeat> seats)
    {
        var mafia = seats.Count(s => s.Alive && s.Role == MafiaRole.Mafia);
        var others = seats.Count(s => s.Alive && s.Role != MafiaRole.Mafia);
        return mafia == 0 ? MafiaTeam.Civilians : mafia >= others ? MafiaTeam.Mafia : null;
    }

    private static MafiaState Next(MafiaState state, DateTimeOffset now) => state.Phase switch
    {
        MafiaPhase.Intro => state with { Phase = MafiaPhase.Morning, Day = 1, NightVictim = null, Deadline = now + MorningTime },
        MafiaPhase.Night => ResolveNight(state, now),
        MafiaPhase.Morning => StartSpeeches(state, now),
        MafiaPhase.Speech => state.SpeakerIndex + 1 < state.SpeechQueue.Count
            ? state with { SpeakerIndex = state.SpeakerIndex + 1, Deadline = now + SpeechTime }
            : state with { Phase = MafiaPhase.Voting, Votes = new Dictionary<Guid, Guid?>(), Deadline = now + VotingTime },
        MafiaPhase.Voting => CountVotes(state, now),
        MafiaPhase.VoteResult => state.Eliminated is not null
            ? state with { Phase = MafiaPhase.LastWord, Deadline = now + LastWordTime }
            : EndOrNight(state, now),
        MafiaPhase.LastWord => EndOrNight(state, now),
        _ => state,
    };

    private static Guid? MafiaTarget(MafiaState state)
    {
        var targets = state.Seats.Where(s => s.Alive && s.Role == MafiaRole.Mafia)
            .Select(s => state.NightActions.TryGetValue(s.UserId, out var t) ? t : (Guid?)null)
            .ToList();
        return targets.Count > 0 && targets.All(t => t is not null && t == targets[0]) ? targets[0] : null;
    }

    private static MafiaState ResolveNight(MafiaState state, DateTimeOffset now)
    {
        var heal = state.Seats.FirstOrDefault(s => s.Alive && s.Role == MafiaRole.Doctor) is { } doctor
            && state.NightActions.TryGetValue(doctor.UserId, out var healed) ? healed : (Guid?)null;
        var target = MafiaTarget(state);
        var victim = target is { } t && t != heal ? t : (Guid?)null;
        var alive = state.Seats.Where(s => s.Alive).ToList();

        // Задания — мирным, живым на начало ночи: убитый этой ночью свои уже заработал.
        var bonuses = alive
            .Where(s => s.Role == MafiaRole.Civilian && state.Tasks.GetValueOrDefault(s.UserId) > 0)
            .Select(s => new MafiaBonus(s.UserId, MafiaBonusKind.Tasks, state.Tasks[s.UserId], state.Tasks[s.UserId]))
            .ToList();
        if (victim is not null)
        {
            bonuses.AddRange(alive.Where(s => s.Role == MafiaRole.Mafia).Select(s => new MafiaBonus(s.UserId, MafiaBonusKind.Kill, 1, KillDonkeys)));
        }
        else if (target is not null && alive.FirstOrDefault(s => s.Role == MafiaRole.Doctor) is { } savior)
        {
            bonuses.Add(new MafiaBonus(savior.UserId, MafiaBonusKind.Save, 1, SaveDonkeys));
        }

        var seats = state.Seats.Select(s => s.UserId == victim ? s with { Alive = false } : s).ToList();
        var next = state with
        {
            Seats = seats,
            Day = state.Day + 1,
            NightActions = new Dictionary<Guid, Guid>(),
            LastHeal = heal,
            NightVictim = victim,
            Bonuses = [.. state.Bonuses, .. bonuses],
        };

        return WinnerOf(seats) is { } winner
            ? next with { Phase = MafiaPhase.Ended, Winner = winner, Deadline = null }
            : next with { Phase = MafiaPhase.Morning, Deadline = now + MorningTime };
    }

    private static MafiaState StartSpeeches(MafiaState state, DateTimeOffset now)
    {
        // Открывает следующий живой по кругу за тем, кто открывал вчера, даже если тот уже выбыл.
        var seats = state.Seats.Select(s => s.UserId).ToList();
        var start = state.SpeechQueue.Count == 0 ? 0 : seats.IndexOf(state.SpeechQueue[0]) + 1;
        var queue = seats.Skip(start).Concat(seats.Take(start))
            .Where(id => state.Seats.First(s => s.UserId == id).Alive)
            .ToList();
        return state with { Phase = MafiaPhase.Speech, SpeechQueue = queue, SpeakerIndex = 0, Deadline = now + SpeechTime };
    }

    private static MafiaState CountVotes(MafiaState state, DateTimeOffset now)
    {
        var tally = state.Votes.Values.OfType<Guid>().GroupBy(t => t).Select(g => (Target: g.Key, Count: g.Count())).ToList();
        var max = tally.Count == 0 ? 0 : tally.Max(t => t.Count);
        var leaders = tally.Where(t => t.Count == max).ToList();
        // Ничья или никто не голосовал — никто не выбывает.
        var eliminated = leaders.Count == 1 ? leaders[0].Target : (Guid?)null;
        var seats = state.Seats.Select(s => s.UserId == eliminated ? s with { Alive = false } : s).ToList();
        var ousted = seats.Any(s => s.UserId == eliminated && s.Role == MafiaRole.Mafia);
        var bonuses = ousted
            ? seats.Where(s => s.Alive && s.Role != MafiaRole.Mafia).Select(s => new MafiaBonus(s.UserId, MafiaBonusKind.Oust, 1, OustDonkeys))
            : Enumerable.Empty<MafiaBonus>();
        return state with
        {
            Phase = MafiaPhase.VoteResult,
            Eliminated = eliminated,
            Seats = seats,
            Bonuses = [.. state.Bonuses, .. bonuses],
            Revealed = eliminated is { } e ? [.. state.Revealed, e] : state.Revealed,
            Deadline = now + VoteResultTime,
        };
    }

    private static MafiaState EndOrNight(MafiaState state, DateTimeOffset now) =>
        WinnerOf(state.Seats) is { } winner
            // Игру решило голосование — вчерашняя ночная жертва в итоге не нужна.
            ? state with { Phase = MafiaPhase.Ended, Winner = winner, NightVictim = null, Deadline = null }
            : state with { Phase = MafiaPhase.Night, NightActions = new Dictionary<Guid, Guid>(), Tasks = new Dictionary<Guid, int>(), TaskAt = new Dictionary<Guid, DateTimeOffset>(), Votes = new Dictionary<Guid, Guid?>(), Eliminated = null, Deadline = now + NightTime };
}
