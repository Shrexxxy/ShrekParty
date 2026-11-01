using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Live;
using ShrekParty.Api.Pets;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Registration;

/// <summary>
/// Исход операции регистрации.
/// </summary>
public enum RegistrationOutcome
{
    Ok,
    NotOpen,
    InvalidInput,
    LoginTaken,
    AttemptNotFound,
    AlreadyCompleted,
    SamePassword,
}

/// <summary>
/// Регистрация-ловушка: открытие, старт с первым паролем, завершение со вторым (любым, но не тем же).
/// </summary>
public partial class RegistrationService(
    AppDbContext db,
    TimeProvider time,
    IPasswordAnalyzer analyzer,
    IPasswordHasher<User> userHasher,
    IPasswordHasher<RegistrationAttempt> attemptHasher,
    DonkeyService donkeys,
    ConnectionTracker connections,
    ILiveNotifier live,
    PetService pets)
{
    /// <summary>До этой оценки первый пароль считается слабым — его маску можно показывать на ТВ.</summary>
    public const int MaxWeakScore = 1;

    /// <summary>Максимум осликов за каждую из двух частей (скорость, пароль).</summary>
    public const int PartMaxDonkeys = 10;

    /// <summary>
    /// Текущий статус регистрации.
    /// </summary>
    public async Task<RegistrationStatusDto> GetStatusAsync()
    {
        var ev = await CurrentEventAsync();
        return new RegistrationStatusDto(ToState(ev), ev?.StartedAt, connections.WaitingCount, ev?.FinishedAt);
    }

    /// <summary>
    /// Таблица регистрации с разбивкой осликов. До закрытия — пустая; опоздавшие — в конце, без мест.
    /// </summary>
    public async Task<IReadOnlyList<RegistrationResultRowDto>> GetResultsAsync()
    {
        var ev = await CurrentEventAsync();
        if (ev is not { Status: EventStatus.Finished })
        {
            return [];
        }

        var openedAt = ev.StartedAt!.Value;
        var closedAt = ev.FinishedAt!.Value;
        var completed = await db.RegistrationAttempts.AsNoTracking().Where(a => a.UserId != null).ToListAsync();
        var attempts = completed.Where(a => a.CompletedAt <= closedAt).ToList();
        var late = completed.Where(a => a.CompletedAt > closedAt).OrderBy(a => a.CompletedAt).ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => completed.Select(a => a.UserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);
        var awards = await db.DonkeyAwards.AsNoTracking()
            .Where(a => a.EventId == ev.Id)
            .GroupBy(a => new { a.UserId, a.Reason })
            .Select(g => new { g.Key.UserId, g.Key.Reason, Total = g.Sum(a => a.Amount) })
            .ToListAsync();

        int Donkeys(Guid userId, string reason) =>
            awards.Where(a => a.UserId == userId && a.Reason == reason).Sum(a => a.Total);

        var speedPlaces = DenseRanking.Rank(attempts, a => a.CompletedAt!.Value - openedAt, descending: false)
            .ToDictionary(r => r.Item.Id, r => r.Place);

        var rows = attempts.Select(a =>
        {
            var userId = a.UserId!.Value;
            var speed = Donkeys(userId, DonkeyReasons.RegistrationSpeed);
            var password = Donkeys(userId, DonkeyReasons.RegistrationPassword);
            var guessesLog10 = a.SecondGuessesLog10 ?? 0;
            return new RegistrationResultRowDto(
                userId,
                a.Login,
                users[userId].ToMe().AvatarUrl,
                Place: 0,
                speedPlaces[a.Id],
                (a.CompletedAt!.Value - openedAt).TotalSeconds,
                ScoreFromGuessesLog10(guessesLog10),
                guessesLog10,
                speed,
                password,
                speed + password);
        }).ToList();

        var ranked = DenseRanking.Rank(rows, r => r.TotalDonkeys, descending: true)
            .Select(r => r.Item with { Place = r.Place })
            .OrderBy(r => r.Place)
            .ThenBy(r => r.SpeedPlace);

        var lateRows = late.Select(a =>
        {
            var guessesLog10 = a.SecondGuessesLog10 ?? 0;
            return new RegistrationResultRowDto(
                a.UserId!.Value,
                a.Login,
                users[a.UserId!.Value].ToMe().AvatarUrl,
                Place: null,
                SpeedPlace: null,
                (a.CompletedAt!.Value - openedAt).TotalSeconds,
                ScoreFromGuessesLog10(guessesLog10),
                guessesLog10,
                SpeedDonkeys: 0,
                PasswordDonkeys: 0,
                TotalDonkeys: 0,
                IsLate: true);
        });

        return ranked.Concat(lateRows).ToList();
    }

    /// <summary>
    /// Оценка 0–4 по числу попыток подбора — те же пороги, что у zxcvbn.
    /// </summary>
    public static int ScoreFromGuessesLog10(double guessesLog10)
    {
        // Пороги zxcvbn: 10³, 10⁶, 10⁸, 10¹⁰ попыток (+5 — запас на округление в самой библиотеке).
        var guesses = Math.Pow(10, guessesLog10);
        double[] thresholds = [1e3 + 5, 1e6 + 5, 1e8 + 5, 1e10 + 5];
        var score = thresholds.TakeWhile(t => guesses >= t).Count();
        return score;
    }

    /// <summary>
    /// Открывает регистрацию (один раз).
    /// </summary>
    public async Task<bool> OpenAsync()
    {
        if (await CurrentEventAsync() is not null)
        {
            return false;
        }

        db.Events.Add(new Event
        {
            Id = Guid.NewGuid(),
            Type = EventType.Registration,
            Title = "Регистрация",
            Status = EventStatus.Active,
            MaxDonkeys = 20,
            Scheme = ScoringScheme.Linear,
            StartedAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
        return true;
    }

    /// <summary>
    /// Закрывает регистрацию и начисляет очки по линейной схеме: скорость и сложность пароля, по 10 осликов максимум.
    /// </summary>
    public async Task<bool> CloseAsync()
    {
        const ScoringScheme scheme = ScoringScheme.Linear;
        var ev = await CurrentEventAsync();
        if (ToState(ev) != RegistrationState.Open)
        {
            return false;
        }

        ev!.Status = EventStatus.Finished;
        ev.FinishedAt = time.GetUtcNow();
        ev.Scheme = scheme;
        await db.SaveChangesAsync();

        var completed = await db.RegistrationAttempts.AsNoTracking()
            .Where(a => a.CompletedAt != null && a.UserId != null)
            .ToListAsync();
        var openedAt = ev.StartedAt!.Value;

        var speed = DenseRanking.Rank(completed, a => a.CompletedAt!.Value - openedAt, descending: false);
        var strength = DenseRanking.Rank(completed, a => a.SecondGuessesLog10 ?? 0, descending: true);

        await donkeys.AwardRankingAsync(ev.Id, ToUsers(speed), scheme, PartMaxDonkeys, DonkeyReasons.RegistrationSpeed);
        await donkeys.AwardRankingAsync(ev.Id, ToUsers(strength), scheme, PartMaxDonkeys, DonkeyReasons.RegistrationPassword);
        await pets.EnsureAsync();
        return true;
    }

    /// <summary>
    /// Шаг 1. Бронирует логин; повтор с тем же логином и паролем возвращает ту же попытку.
    /// </summary>
    public async Task<(RegistrationOutcome Outcome, Guid AttemptId)> StartAsync(string? login, string? password)
    {
        if (ToState(await CurrentEventAsync()) == RegistrationState.Closed)
        {
            return (RegistrationOutcome.NotOpen, default);
        }

        login = login?.Trim() ?? "";
        if (!LoginRegex().IsMatch(login) || string.IsNullOrEmpty(password))
        {
            return (RegistrationOutcome.InvalidInput, default);
        }

        var normalized = User.Normalize(login);
        if (await db.Users.AnyAsync(u => u.LoginNormalized == normalized))
        {
            return (RegistrationOutcome.LoginTaken, default);
        }

        var existing = await db.RegistrationAttempts.SingleOrDefaultAsync(a => a.LoginNormalized == normalized);
        if (existing is not null)
        {
            var samePerson = existing.CompletedAt is null
                && attemptHasher.VerifyHashedPassword(existing, existing.FirstPasswordHash, password) != PasswordVerificationResult.Failed;
            return samePerson ? (RegistrationOutcome.Ok, existing.Id) : (RegistrationOutcome.LoginTaken, default);
        }

        var analysis = analyzer.Analyze(password);
        var attempt = new RegistrationAttempt
        {
            Id = Guid.NewGuid(),
            Login = login,
            LoginNormalized = normalized,
            FirstPasswordHash = "",
            FirstScore = analysis.Score,
            FirstGuessesLog10 = analysis.GuessesLog10,
            FirstPatternsJson = JsonSerializer.Serialize(analysis.Patterns),
            FirstMask = analysis.Score <= MaxWeakScore ? PasswordMask.Mask(password) : null,
            StartedAt = time.GetUtcNow(),
        };
        attempt.FirstPasswordHash = attemptHasher.HashPassword(attempt, password);
        db.RegistrationAttempts.Add(attempt);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Двое одновременно взяли один логин — уникальный индекс решил за нас.
            return (RegistrationOutcome.LoginTaken, default);
        }

        return (RegistrationOutcome.Ok, attempt.Id);
    }

    /// <summary>
    /// Шаг 2. Создаёт игрока со вторым паролем и стартовым капиталом.
    /// </summary>
    public async Task<(RegistrationOutcome Outcome, User? User)> CompleteAsync(Guid attemptId, string? newPassword)
    {
        if (ToState(await CurrentEventAsync()) == RegistrationState.Closed)
        {
            return (RegistrationOutcome.NotOpen, null);
        }

        var attempt = await db.RegistrationAttempts.FindAsync(attemptId);
        if (attempt is null)
        {
            return (RegistrationOutcome.AttemptNotFound, null);
        }

        if (attempt.CompletedAt is not null)
        {
            return (RegistrationOutcome.AlreadyCompleted, null);
        }

        newPassword ??= "";
        if (attemptHasher.VerifyHashedPassword(attempt, attempt.FirstPasswordHash, newPassword) != PasswordVerificationResult.Failed)
        {
            return (RegistrationOutcome.SamePassword, null);
        }

        // Слабый тоже принимаем: гость просто получит меньше осликов за пароль.
        var analysis = analyzer.Analyze(newPassword);

        var now = time.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = attempt.Login,
            LoginNormalized = attempt.LoginNormalized,
            PasswordHash = "",
            Role = UserRole.Player,
            CreatedAt = now,
        };
        user.PasswordHash = userHasher.HashPassword(user, newPassword);
        db.Users.Add(user);
        attempt.CompletedAt = now;
        attempt.SecondGuessesLog10 = analysis.GuessesLog10;
        attempt.UserId = user.Id;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return (RegistrationOutcome.AlreadyCompleted, null);
        }

        await donkeys.AwardAsync(user.Id, 150, DonkeyReasons.StartCapital);

        // Опоздавший после закрытия получает яйцо сразу; остальные — при закрытии.
        if (ToState(await CurrentEventAsync()) == RegistrationState.Finished)
        {
            await pets.EnsureAsync();
        }

        return (RegistrationOutcome.Ok, user);
    }

    private static List<Ranked<Guid>> ToUsers(IReadOnlyList<Ranked<RegistrationAttempt>> ranking) =>
        ranking.Select(r => new Ranked<Guid>(r.Item.UserId!.Value, r.Place)).ToList();

    private Task<Event?> CurrentEventAsync() =>
        db.Events.SingleOrDefaultAsync(e => e.Type == EventType.Registration);

    private static RegistrationState ToState(Event? ev) => ev?.Status switch
    {
        null => RegistrationState.Closed,
        EventStatus.Finished => RegistrationState.Finished,
        _ => RegistrationState.Open,
    };

    [GeneratedRegex(@"^[\p{L}\d_]{3,20}$")]
    private static partial Regex LoginRegex();
}
