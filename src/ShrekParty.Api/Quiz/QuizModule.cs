using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Quiz;

/// <summary>Фаза Своей игры.</summary>
public enum QuizPhase
{
    /// <summary>ТВ крутит рулетку — кто выбирает первым.</summary>
    Roulette,

    /// <summary>Выбирающий выбирает клетку.</summary>
    Choosing,

    /// <summary>Вопрос читается, кнопка открыта.</summary>
    Reading,

    /// <summary>Отвечает нажавший первым.</summary>
    Answering,

    /// <summary>Вопрос закрыт, ответ на экране до следующего выбора.</summary>
    Closed,

    /// <summary>Раунды сыграны, ждём «Финал» от именинника.</summary>
    Done,

    /// <summary>Финал: тема видна, финалисты ставят.</summary>
    FinalBetting,

    /// <summary>Финал: вопрос, ответ текстом.</summary>
    FinalQuestion,

    /// <summary>Финал: именинник отмечает верные.</summary>
    FinalJudging,

    /// <summary>Финал: раскрытие от меньшего счёта к большему.</summary>
    FinalReveal,
}

/// <summary>Команда медиа для ТВ; номер растёт, чтобы ТВ отличало новую команду от старой.</summary>
public record QuizMediaCommand(QuizMediaAction Action, int Seq);

/// <summary>Что сделать с аудио/видео на ТВ.</summary>
public enum QuizMediaAction
{
    Play,
    Pause,
}

/// <summary>Состояние Своей игры в Event.StateJson.</summary>
/// <param name="Rounds">Номера раундов с вопросами, по порядку (финал не входит).</param>
/// <param name="OpenSince">С какого момента кнопка открыта — нажатие раньше считается этим моментом.</param>
/// <param name="PressWindowEndsAt">Окно сбора нажатий после первого; <c>null</c> — нажатий ещё не было.</param>
public record QuizState(
    QuizPhase Phase,
    int Round,
    IReadOnlyList<int> Rounds,
    Guid? ChooserId,
    DateTimeOffset? Deadline,
    Guid? QuestionId,
    DateTimeOffset? ReadingStartedAt,
    DateTimeOffset? OpenSince,
    Guid? AnswererId,
    IReadOnlyList<Guid> Wrong,
    IReadOnlyList<Guid> Played,
    DateTimeOffset? PressWindowEndsAt,
    QuizMediaCommand? MediaCommand);

/// <summary>Клетка табло.</summary>
public record QuizCellDto(Guid QuestionId, int Price, bool Played);

/// <summary>Тема на табло текущего раунда.</summary>
public record QuizBoardThemeDto(Guid ThemeId, string Name, IReadOnlyList<QuizCellDto> Cells);

/// <summary>Текущий вопрос; адрес аудио/видео — только ТВ и админу, картинка — всем.</summary>
public record QuizQuestionViewDto(Guid Id, string ThemeName, int Price, string Text, QuizMediaType? MediaType, string? MediaUrl);

/// <summary>Счёт игрока.</summary>
public record QuizScoreDto(Guid UserId, string Login, string? AvatarUrl, int Score);

/// <summary>Финалист; ставка, ответ и отметка чужих — только админу и с раскрытия.</summary>
public record QuizFinalistDto(Guid UserId, string Login, string? AvatarUrl, int ScoreBefore, int? Bet, string? Answer, bool? Correct);

/// <summary>Финал: тема и финалисты в порядке раскрытия (от меньшего счёта).</summary>
public record QuizFinalDto(string ThemeName, IReadOnlyList<QuizFinalistDto> Finalists, bool IAmFinalist, int? MyBet, string? MyAnswer);

/// <summary>Детали Своей игры.</summary>
/// <param name="Answer">Ответ — админу всегда, остальным после закрытия вопроса.</param>
public record QuizDetails(
    QuizPhase Phase,
    int Round,
    IReadOnlyList<int> Rounds,
    IReadOnlyList<QuizBoardThemeDto> Board,
    Guid? ChooserId,
    DateTimeOffset? Deadline,
    QuizQuestionViewDto? Question,
    DateTimeOffset? ReadingStartedAt,
    Guid? AnswererId,
    IReadOnlyList<Guid> Wrong,
    string? Answer,
    IReadOnlyList<QuizScoreDto> Scores,
    QuizMediaCommand? MediaCommand,
    QuizFinalDto? Final);

/// <summary>
/// Своя игра: рулетка → выбор клетки → чтение с кнопкой → ответ → … по раундам.
/// Переходы по времени (рулетка, таймаут ответа, окно нажатий) делает тик — под той же блокировкой, что и действия.
/// </summary>
public class QuizModule(AppDbContext db, ILiveNotifier live, Random random, TimeProvider time) : IEventModule
{
    /// <summary>Сколько крутится рулетка первого выбирающего.</summary>
    public static readonly TimeSpan RouletteTime = TimeSpan.FromSeconds(5);

    /// <summary>Время на ответ вслух.</summary>
    public static readonly TimeSpan AnswerTime = TimeSpan.FromSeconds(10);

    /// <summary>Время на ставку в финале.</summary>
    public static readonly TimeSpan FinalBetTime = TimeSpan.FromSeconds(30);

    /// <summary>Время на текстовый ответ в финале.</summary>
    public static readonly TimeSpan FinalAnswerTime = TimeSpan.FromSeconds(60);

    /// <summary>Сколько после первого нажатия ждём остальных — побеждает самое раннее по часам.</summary>
    public static readonly TimeSpan PressWindow = TimeSpan.FromMilliseconds(150);

    /// <summary>Одна блокировка на все изменения игры: действия игроков, админа и тик.</summary>
    internal static readonly SemaphoreSlim Lock = new(1, 1);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Quiz;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        var players = await db.Users.Where(u => u.Role == UserRole.Player).Select(u => u.Id).ToListAsync();
        if (players.Count == 0)
        {
            throw new EventRuleException("not_enough_players");
        }

        var rounds = await RoundsWithQuestionsAsync(db);
        if (rounds.Count == 0)
        {
            throw new EventRuleException("no_questions");
        }

        SetState(ev, new QuizState(
            QuizPhase.Roulette, rounds[0], rounds, players[random.Next(players.Count)], time.GetUtcNow() + RouletteTime,
            null, null, null, null, [], [], null, null));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var themes = await db.QuizThemes.AsNoTracking().Where(t => t.Round == state.Round).OrderBy(t => t.Order).ToListAsync();
        var themeIds = themes.Select(t => t.Id).ToList();
        var questions = await db.QuizQuestions.AsNoTracking()
            .Where(q => themeIds.Contains(q.ThemeId) || q.Id == state.QuestionId)
            .ToListAsync();

        var board = themes
            .Select(t => new QuizBoardThemeDto(t.Id, t.Name, questions
                .Where(q => q.ThemeId == t.Id)
                .OrderBy(q => q.Order)
                .Select(q => new QuizCellDto(q.Id, q.Price, state.Played.Contains(q.Id)))
                .ToList()))
            .Where(t => t.Cells.Count > 0)
            .ToList();

        QuizQuestionViewDto? question = null;
        string? answer = null;
        if (questions.FirstOrDefault(q => q.Id == state.QuestionId) is { } current)
        {
            var themeName = (await db.QuizThemes.AsNoTracking().FirstAsync(t => t.Id == current.ThemeId)).Name;
            // Аудио и видео играют только на ТВ — телефонам адрес не нужен.
            var showMedia = current.MediaType == QuizMediaType.Image || viewerId is null;
            question = new QuizQuestionViewDto(
                current.Id, themeName, current.Price, current.Text, current.MediaType,
                showMedia && current.MediaId is { } media ? MediaStorage.UrlFor(media) : null);
            // После последнего вопроса раундов ответ остаётся на экране до финала.
            answer = isAdmin || state.Phase is QuizPhase.Closed or QuizPhase.Done or QuizPhase.FinalReveal ? current.Answer : null;
        }

        return new QuizDetails(
            state.Phase, state.Round, state.Rounds, board, state.ChooserId, state.Deadline, question, state.ReadingStartedAt,
            state.AnswererId, state.Wrong, answer, await ScoresAsync(db, ev.Id), state.MediaCommand,
            await FinalAsync(ev.Id, state, viewerId, isAdmin));
    }

    /// <inheritdoc/>
    public async Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        await Lock.WaitAsync();
        try
        {
            // Действие могло поменять игру, пока движок читал ивент.
            await db.Entry(ev).ReloadAsync();
            if (ev.Status != EventStatus.Active)
            {
                return false;
            }

            var state = StateOf(ev);
            var next = state.Phase switch
            {
                QuizPhase.Roulette when now >= state.Deadline => state with { Phase = QuizPhase.Choosing, Deadline = null },
                QuizPhase.Reading when state.PressWindowEndsAt <= now => await ResolvePressesAsync(db, ev.Id, state, now),
                QuizPhase.Answering when now >= state.Deadline => await WrongAsync(db, ev.Id, state, now),
                QuizPhase.FinalBetting when now >= state.Deadline => await FinalQuestionAsync(db, ev.Id, state, now),
                QuizPhase.FinalQuestion when now >= state.Deadline => await FinalJudgingAsync(db, ev.Id, state),
                _ => state,
            };

            if (next != state)
            {
                SetState(ev, next);
                await db.SaveChangesAsync();
                await live.BroadcastAsync();
            }

            return false;
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var scores = await ScoresAsync(db, ev.Id);
        return DenseRanking.Rank(scores, s => s.Score, descending: true)
            .Select(r => new Ranked<Guid>(r.Item.UserId, r.Place))
            .ToList();
    }

    internal static QuizState StateOf(Event ev) => JsonSerializer.Deserialize<QuizState>(ev.StateJson!, Json)!;

    internal static void SetState(Event ev, QuizState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);

    /// <summary>
    /// Окно нажатий закрылось: отвечает самый ранний из не ошибавшихся на этот вопрос.
    /// </summary>
    internal static async Task<QuizState> ResolvePressesAsync(AppDbContext db, Guid eventId, QuizState state, DateTimeOffset now)
    {
        var presses = await db.QuizPresses.Where(p => p.EventId == eventId && p.QuestionId == state.QuestionId).ToListAsync();
        var winner = presses
            .Where(p => !state.Wrong.Contains(p.UserId))
            .OrderBy(p => p.PressedAt)
            .ThenBy(p => p.Seq)
            .FirstOrDefault();
        return winner is null
            ? state with { PressWindowEndsAt = null }
            : state with { Phase = QuizPhase.Answering, AnswererId = winner.UserId, Deadline = now + AnswerTime, PressWindowEndsAt = null };
    }

    /// <summary>
    /// Неверно или время вышло: минус цена, кнопка снова открыта для остальных.
    /// </summary>
    internal static async Task<QuizState> WrongAsync(AppDbContext db, Guid eventId, QuizState state, DateTimeOffset now)
    {
        var question = await db.QuizQuestions.FirstAsync(q => q.Id == state.QuestionId);
        db.QuizScoreEntries.Add(new QuizScoreEntry { Id = Guid.NewGuid(), EventId = eventId, UserId = state.AnswererId!.Value, QuestionId = question.Id, Delta = -question.Price });
        // Прежние нажатия не в счёт: после ошибки остальные жмут заново.
        await db.QuizPresses.Where(p => p.EventId == eventId && p.QuestionId == question.Id).ExecuteDeleteAsync();
        return state with
        {
            Phase = QuizPhase.Reading,
            AnswererId = null,
            Deadline = null,
            Wrong = [.. state.Wrong, state.AnswererId.Value],
            OpenSince = now,
        };
    }

    /// <summary>
    /// Вопрос сыгран; если клеток в раунде не осталось (или <paramref name="force"/>) — следующий раунд, после последнего — конец раундов.
    /// </summary>
    internal static async Task<QuizState> NextRoundIfDoneAsync(AppDbContext db, QuizState state, bool force)
    {
        var themeIds = await db.QuizThemes.Where(t => t.Round == state.Round).Select(t => t.Id).ToListAsync();
        var left = await db.QuizQuestions.Where(q => themeIds.Contains(q.ThemeId)).Select(q => q.Id).ToListAsync();
        if (!force && left.Any(id => !state.Played.Contains(id)))
        {
            return state;
        }

        var index = state.Rounds.ToList().IndexOf(state.Round);
        return index + 1 < state.Rounds.Count
            ? state with { Round = state.Rounds[index + 1] }
            : state with { Phase = QuizPhase.Done };
    }

    /// <summary>
    /// Ставки сделаны: кто не поставил — ставка 1 (минимум по правилам), дальше вопрос.
    /// </summary>
    internal static async Task<QuizState> FinalQuestionAsync(AppDbContext db, Guid eventId, QuizState state, DateTimeOffset now)
    {
        await db.QuizFinalAnswers.Where(f => f.EventId == eventId && f.Bet == null).ExecuteUpdateAsync(s => s.SetProperty(f => f.Bet, 1));
        var question = await FinalQuestionOfAsync(db);
        return state with { Phase = QuizPhase.FinalQuestion, QuestionId = question?.Id, Deadline = now + FinalAnswerTime };
    }

    /// <summary>
    /// Время ответа вышло: пустой ответ — сразу неверно, остальные отмечает именинник.
    /// </summary>
    internal static async Task<QuizState> FinalJudgingAsync(AppDbContext db, Guid eventId, QuizState state)
    {
        await db.QuizFinalAnswers.Where(f => f.EventId == eventId && (f.Text == null || f.Text == ""))
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Correct, false));
        return state with { Phase = QuizPhase.FinalJudging, Deadline = null };
    }

    internal static async Task<QuizQuestion?> FinalQuestionOfAsync(AppDbContext db)
    {
        var theme = await db.QuizThemes.FirstOrDefaultAsync(t => t.Round == QuizContentService.FinalRound);
        return theme is null ? null : await db.QuizQuestions.FirstOrDefaultAsync(q => q.ThemeId == theme.Id);
    }

    internal static async Task<IReadOnlyList<QuizScoreDto>> ScoresAsync(AppDbContext db, Guid eventId)
    {
        var sums = await db.QuizScoreEntries.Where(s => s.EventId == eventId)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(s => s.Delta) })
            .ToDictionaryAsync(s => s.UserId, s => s.Score);
        return (await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync())
            .Select(u => new QuizScoreDto(u.Id, u.Login, u.ToMe().AvatarUrl, sums.GetValueOrDefault(u.Id)))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<QuizFinalDto?> FinalAsync(Guid eventId, QuizState state, Guid? viewerId, bool isAdmin)
    {
        if (state.Phase is not (QuizPhase.FinalBetting or QuizPhase.FinalQuestion or QuizPhase.FinalJudging or QuizPhase.FinalReveal))
        {
            return null;
        }

        var themeName = (await db.QuizThemes.AsNoTracking().FirstOrDefaultAsync(t => t.Round == QuizContentService.FinalRound))?.Name ?? "";
        var answers = await db.QuizFinalAnswers.AsNoTracking().Where(f => f.EventId == eventId).ToListAsync();
        var userIds = answers.Select(a => a.UserId).ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var open = isAdmin || state.Phase == QuizPhase.FinalReveal;
        var mine = answers.FirstOrDefault(a => a.UserId == viewerId);

        return new QuizFinalDto(
            themeName,
            answers.Where(a => users.ContainsKey(a.UserId))
                .OrderBy(a => a.ScoreBefore)
                .ThenBy(a => users[a.UserId].Login, StringComparer.OrdinalIgnoreCase)
                .Select(a => new QuizFinalistDto(
                    a.UserId, users[a.UserId].Login, users[a.UserId].ToMe().AvatarUrl, a.ScoreBefore,
                    open ? a.Bet : null, open ? a.Text : null, open ? a.Correct : null))
                .ToList(),
            mine is not null,
            mine?.Bet,
            mine?.Text);
    }

    private static async Task<List<int>> RoundsWithQuestionsAsync(AppDbContext db)
    {
        var themeRounds = await db.QuizThemes.Where(t => t.Round != QuizContentService.FinalRound).ToDictionaryAsync(t => t.Id, t => t.Round);
        var withQuestions = await db.QuizQuestions.Select(q => q.ThemeId).Distinct().ToListAsync();
        return withQuestions.Where(themeRounds.ContainsKey).Select(id => themeRounds[id]).Distinct().Order().ToList();
    }
}
