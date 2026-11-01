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

/// <summary>Игрок за столом.</summary>
public record MafiaSeat(Guid UserId, MafiaRole Role, bool Alive);

/// <summary>Проверка комиссара.</summary>
public record MafiaCheck(int Day, Guid TargetId, bool IsMafia);

/// <summary>Состояние мафии в Event.StateJson — целиком, без отдельных таблиц.</summary>
/// <param name="NightActions">Ходы этой ночи: кто → на кого.</param>
/// <param name="LastHeal">Кого доктор лечил прошлой ночью — второй раз подряд нельзя.</param>
/// <param name="Revealed">Чьи роли открыты всем (выбывшие голосованием).</param>
/// <param name="Step">Номер перехода: «Дальше» ведущего с устаревшим номером отклоняется — таймер успел раньше.</param>
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
    int Step = 0);

/// <summary>Игрок в деталях; роль — только тем, кому положено её видеть.</summary>
public record MafiaPlayerDto(Guid UserId, string Login, string? AvatarUrl, bool Alive, MafiaRole? Role);

/// <summary>Ход ночи (кто → на кого).</summary>
public record MafiaActionDto(Guid UserId, Guid TargetId);

/// <summary>Голос; null — воздержался.</summary>
public record MafiaVoteDto(Guid VoterId, Guid? TargetId);

/// <summary>Выбывший голосованием и его роль.</summary>
public record MafiaEliminatedDto(Guid UserId, MafiaRole Role);

/// <summary>Детали мафии.</summary>
/// <param name="NightActions">Ведущему — все ходы ночи, мафии — ходы напарников, остальным — пусто.</param>
/// <param name="Checks">Комиссару — его проверки, ведущему — все.</param>
/// <param name="Votes">Голоса — после голосования всем, ведущему — сразу.</param>
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
    MafiaTeam? Winner);

/// <summary>
/// Мафия: знакомство → утро → речи → голосование → (последнее слово) → ночь → утро → …
/// Фазы идут по таймерам, ведущий может закончить любую раньше; ночь и голосование кончаются сами, когда все сходили.
/// </summary>
public class MafiaModule(AppDbContext db, ILiveNotifier live, Random random) : IEventModule
{
    /// <summary>Меньше — не игра: 2 мафии, доктор, комиссар и хотя бы один мирный.</summary>
    public const int MinPlayers = 5;

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

    /// <summary>Одна блокировка на ходы, голоса, «Дальше» и тик.</summary>
    internal static readonly SemaphoreSlim Lock = new(1, 1);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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
            new Dictionary<Guid, Guid>(), null, [], null, [], 0, new Dictionary<Guid, Guid?>(), null, [], null));
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
            state.Winner);
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
    public Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var state = StateOf(ev);
        if (state.Winner is not { } winner)
        {
            // Игру прервали — без мест и осликов.
            return Task.FromResult<IReadOnlyList<Ranked<Guid>>>([]);
        }

        // Победа командная: мёртвые победители тоже на 1-м месте.
        return Task.FromResult<IReadOnlyList<Ranked<Guid>>>(state.Seats
            .Select(s => new Ranked<Guid>(s.UserId, TeamOf(s.Role) == winner ? 1 : 2))
            .OrderBy(r => r.Place)
            .ToList());
    }

    internal static MafiaState StateOf(Event ev) => JsonSerializer.Deserialize<MafiaState>(ev.StateJson!, Json)!;

    internal static void SetState(Event ev, MafiaState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);

    internal static MafiaTeam TeamOf(MafiaRole role) => role == MafiaRole.Mafia ? MafiaTeam.Mafia : MafiaTeam.Civilians;

    /// <summary>
    /// Следующая фаза — по таймеру, по «Дальше» ведущего или когда все сходили.
    /// </summary>
    internal static MafiaState Advance(MafiaState state, DateTimeOffset now) => Next(state, now) with { Step = state.Step + 1 };

    /// <summary>
    /// Ночь кончится раньше, когда сходили все живые и мафия выбрала одну жертву.
    /// </summary>
    internal static bool NightComplete(MafiaState state)
    {
        var alive = state.Seats.Where(s => s.Alive).ToList();
        return alive.All(s => state.NightActions.ContainsKey(s.UserId)) && MafiaTarget(state) is not null;
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
        var victim = MafiaTarget(state) is { } target && target != heal ? target : (Guid?)null;
        var seats = state.Seats.Select(s => s.UserId == victim ? s with { Alive = false } : s).ToList();
        var next = state with
        {
            Seats = seats,
            Day = state.Day + 1,
            NightActions = new Dictionary<Guid, Guid>(),
            LastHeal = heal,
            NightVictim = victim,
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
        return state with
        {
            Phase = MafiaPhase.VoteResult,
            Eliminated = eliminated,
            Seats = state.Seats.Select(s => s.UserId == eliminated ? s with { Alive = false } : s).ToList(),
            Revealed = eliminated is { } e ? [.. state.Revealed, e] : state.Revealed,
            Deadline = now + VoteResultTime,
        };
    }

    private static MafiaState EndOrNight(MafiaState state, DateTimeOffset now) =>
        WinnerOf(state.Seats) is { } winner
            // Игру решило голосование — вчерашняя ночная жертва в итоге не нужна.
            ? state with { Phase = MafiaPhase.Ended, Winner = winner, NightVictim = null, Deadline = null }
            : state with { Phase = MafiaPhase.Night, NightActions = new Dictionary<Guid, Guid>(), Votes = new Dictionary<Guid, Guid?>(), Eliminated = null, Deadline = now + NightTime };
}
