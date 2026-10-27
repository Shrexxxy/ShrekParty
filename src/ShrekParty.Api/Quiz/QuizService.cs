using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Quiz;

/// <summary>
/// Действия в Своей игре: выбор клетки, кнопка, вердикт, закрыть вопрос, следующий раунд, команды медиа.
/// Всё — под <see cref="QuizModule.Lock"/>, чтобы не гоняться с тиком.
/// </summary>
public class QuizService(AppDbContext db, ILiveNotifier live, TimeProvider time)
{
    /// <summary>
    /// Выбор клетки: игрок — только если он выбирающий (иначе 403), админ — за него (<paramref name="userId"/> = null).
    /// </summary>
    public Task ChooseAsync(Guid eventId, Guid questionId, Guid? userId) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase is not (QuizPhase.Choosing or QuizPhase.Closed))
        {
            throw new EventRuleException("not_choosing");
        }
        if (userId is not null && userId != state.ChooserId)
        {
            throw new UnauthorizedAccessException();
        }

        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId) ?? throw new KeyNotFoundException();
        var theme = await db.QuizThemes.FirstAsync(t => t.Id == question.ThemeId);
        if (theme.Round != state.Round || state.Played.Contains(questionId))
        {
            throw new EventRuleException("played");
        }

        var now = time.GetUtcNow();
        return state with
        {
            Phase = QuizPhase.Reading,
            QuestionId = questionId,
            ReadingStartedAt = now,
            OpenSince = now,
            AnswererId = null,
            Deadline = null,
            Wrong = [],
            PressWindowEndsAt = null,
            MediaCommand = null,
        };
    });

    /// <summary>
    /// Нажатие кнопки. Первое открывает окно сбора: запрос ждёт его конца и выбирает самого раннего по часам.
    /// </summary>
    /// <param name="clientTime">Момент нажатия, пересчитанный телефоном на часы сервера.</param>
    public async Task PressAsync(Guid eventId, Guid userId, DateTimeOffset? clientTime)
    {
        DateTimeOffset? windowEnds = null;
        await QuizModule.Lock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.Quiz);
            var state = QuizModule.StateOf(ev);
            if (state.Phase != QuizPhase.Reading)
            {
                throw new EventRuleException("not_reading");
            }
            if (state.Wrong.Contains(userId))
            {
                throw new EventRuleException("already_wrong");
            }

            var presses = await db.QuizPresses.Where(p => p.EventId == eventId && p.QuestionId == state.QuestionId).ToListAsync();
            if (presses.Any(p => p.UserId == userId))
            {
                return;
            }

            // Часы телефона могли уйти: не раньше открытия кнопки и не позже прихода запроса.
            var now = time.GetUtcNow();
            var pressedAt = clientTime ?? now;
            pressedAt = pressedAt < state.OpenSince ? state.OpenSince.Value : pressedAt > now ? now : pressedAt;
            db.QuizPresses.Add(new QuizPress
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                QuestionId = state.QuestionId!.Value,
                UserId = userId,
                PressedAt = pressedAt,
                Seq = presses.Count,
            });

            if (state.PressWindowEndsAt is null)
            {
                windowEnds = now + QuizModule.PressWindow;
                QuizModule.SetState(ev, state with { PressWindowEndsAt = windowEnds });
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            QuizModule.Lock.Release();
        }

        if (windowEnds is null)
        {
            return;
        }

        await Task.Delay(QuizModule.PressWindow, time);
        await ChangeAsync(eventId, state =>
            state.Phase == QuizPhase.Reading && state.PressWindowEndsAt <= time.GetUtcNow()
                ? QuizModule.ResolvePressesAsync(db, eventId, state, time.GetUtcNow())
                : Task.FromResult(state));
    }

    /// <summary>
    /// Вердикт админа: верно — плюс цена и право выбора, неверно — минус цена и кнопка для остальных.
    /// </summary>
    public Task VerdictAsync(Guid eventId, bool correct) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != QuizPhase.Answering)
        {
            throw new EventRuleException("not_answering");
        }
        if (!correct)
        {
            return await QuizModule.WrongAsync(db, eventId, state, time.GetUtcNow());
        }

        var question = await db.QuizQuestions.FirstAsync(q => q.Id == state.QuestionId);
        db.QuizScoreEntries.Add(new QuizScoreEntry { Id = Guid.NewGuid(), EventId = eventId, UserId = state.AnswererId!.Value, QuestionId = question.Id, Delta = question.Price });
        var closed = state with
        {
            Phase = QuizPhase.Closed,
            ChooserId = state.AnswererId,
            Deadline = null,
            Played = [.. state.Played, question.Id],
        };
        return await QuizModule.NextRoundIfDoneAsync(db, closed, force: false);
    });

    /// <summary>
    /// Закрыть вопрос без верного ответа: показываем ответ, выбирает тот же.
    /// </summary>
    public Task CloseAsync(Guid eventId) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != QuizPhase.Reading)
        {
            throw new EventRuleException("not_reading");
        }

        var closed = state with { Phase = QuizPhase.Closed, PressWindowEndsAt = null, Played = [.. state.Played, state.QuestionId!.Value] };
        return await QuizModule.NextRoundIfDoneAsync(db, closed, force: false);
    });

    /// <summary>
    /// Следующий раунд, не доигрывая этот.
    /// </summary>
    public Task NextRoundAsync(Guid eventId) => ChangeAsync(eventId, state =>
        state.Phase is QuizPhase.Choosing or QuizPhase.Closed
            ? QuizModule.NextRoundIfDoneAsync(db, state, force: true)
            : throw new EventRuleException("not_choosing"));

    /// <summary>
    /// Команда аудио/видео для ТВ.
    /// </summary>
    public Task MediaAsync(Guid eventId, QuizMediaAction action) => ChangeAsync(eventId, state =>
        state.QuestionId is null
            ? throw new EventRuleException("no_question")
            : Task.FromResult(state with { MediaCommand = new QuizMediaCommand(action, (state.MediaCommand?.Seq ?? 0) + 1) }));

    /// <summary>
    /// Финал после раундов: играют со счётом больше нуля; некому или нет вопроса — сразу раскрытие.
    /// </summary>
    public Task StartFinalAsync(Guid eventId) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != QuizPhase.Done)
        {
            throw new EventRuleException("rounds_not_done");
        }

        var finalists = (await QuizModule.ScoresAsync(db, eventId)).Where(s => s.Score > 0).ToList();
        db.QuizFinalAnswers.AddRange(finalists.Select(f => new QuizFinalAnswer { Id = Guid.NewGuid(), EventId = eventId, UserId = f.UserId, ScoreBefore = f.Score }));
        if (finalists.Count == 0 || await QuizModule.FinalQuestionOfAsync(db) is null)
        {
            return state with { Phase = QuizPhase.FinalReveal, QuestionId = null, Deadline = null };
        }

        return state with { Phase = QuizPhase.FinalBetting, QuestionId = null, Deadline = time.GetUtcNow() + QuizModule.FinalBetTime, MediaCommand = null };
    });

    /// <summary>
    /// Ставка финалиста: от 1 до своего счёта, можно менять до конца времени.
    /// </summary>
    public Task BetAsync(Guid eventId, Guid userId, int amount) => ChangeFinalistAsync(eventId, userId, QuizPhase.FinalBetting, finalist =>
    {
        if (amount < 1 || amount > finalist.ScoreBefore)
        {
            throw new BadInputException("bad_bet");
        }

        finalist.Bet = amount;
    });

    /// <summary>
    /// Ответ финалиста текстом; можно менять до конца времени.
    /// </summary>
    public Task FinalAnswerAsync(Guid eventId, Guid userId, string? text) => ChangeFinalistAsync(eventId, userId, QuizPhase.FinalQuestion, finalist =>
        finalist.Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim());

    /// <summary>
    /// Отметка именинника по финалисту (можно передумать до раскрытия).
    /// </summary>
    public Task FinalVerdictAsync(Guid eventId, Guid userId, bool correct) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != QuizPhase.FinalJudging)
        {
            throw new EventRuleException("not_judging");
        }

        var finalist = await db.QuizFinalAnswers.FirstOrDefaultAsync(f => f.EventId == eventId && f.UserId == userId) ?? throw new KeyNotFoundException();
        finalist.Correct = correct;
        return state;
    });

    /// <summary>
    /// Раскрытие: все отмечены — ставки идут в счёт.
    /// </summary>
    public Task RevealAsync(Guid eventId) => ChangeAsync(eventId, async state =>
    {
        if (state.Phase != QuizPhase.FinalJudging)
        {
            throw new EventRuleException("not_judging");
        }

        var finalists = await db.QuizFinalAnswers.Where(f => f.EventId == eventId).ToListAsync();
        if (finalists.Any(f => f.Correct is null))
        {
            throw new EventRuleException("not_judged");
        }

        db.QuizScoreEntries.AddRange(finalists.Select(f => new QuizScoreEntry
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = f.UserId,
            QuestionId = state.QuestionId,
            Delta = f.Correct == true ? f.Bet ?? 0 : -(f.Bet ?? 0),
        }));
        return state with { Phase = QuizPhase.FinalReveal };
    });

    private async Task ChangeFinalistAsync(Guid eventId, Guid userId, QuizPhase phase, Action<QuizFinalAnswer> change)
    {
        await QuizModule.Lock.WaitAsync();
        try
        {
            var state = QuizModule.StateOf(await db.ActiveOfTypeAsync(eventId, EventType.Quiz, tracking: false));
            if (state.Phase != phase || time.GetUtcNow() >= state.Deadline)
            {
                throw new EventRuleException("too_late");
            }

            var finalist = await db.QuizFinalAnswers.FirstOrDefaultAsync(f => f.EventId == eventId && f.UserId == userId)
                ?? throw new UnauthorizedAccessException();
            change(finalist);
            await db.SaveChangesAsync();
        }
        finally
        {
            QuizModule.Lock.Release();
        }

        await live.BroadcastAsync();
    }

    private async Task ChangeAsync(Guid eventId, Func<QuizState, Task<QuizState>> change)
    {
        await QuizModule.Lock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.Quiz);
            var state = QuizModule.StateOf(ev);
            var next = await change(state);
            if (next == state && !db.ChangeTracker.HasChanges())
            {
                return;
            }

            QuizModule.SetState(ev, next);
            await db.SaveChangesAsync();
        }
        finally
        {
            QuizModule.Lock.Release();
        }

        await live.BroadcastAsync();
    }
}
