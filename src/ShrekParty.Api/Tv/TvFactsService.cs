using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Registration;

namespace ShrekParty.Api.Tv;

/// <summary>
/// Факт для карусели на ТВ.
/// </summary>
/// <param name="Text">Текст факта.</param>
/// <param name="Logins">Логины, упомянутые в тексте, — фронт выделяет их цветом.</param>
public record FactDto(string Text, IReadOnlyList<string> Logins);

/// <summary>
/// Смешные факты из данных регистрации.
/// </summary>
public class TvFactsService(AppDbContext db)
{
    private static readonly Dictionary<string, string> PatternPhrases = new()
    {
        [ZxcvbnPasswordAnalyzer.CommonPasswordPattern] = "— один из самых популярных паролей в мире",
        ["date"] = "похож на дату",
        ["sequence"] = "— это последовательность вроде 12345",
        ["spatial"] = "набран дорожкой по клавиатуре",
        ["dictionary"] = "содержит словарное слово",
        ["repeat"] = "повторяет одно и то же",
    };

    /// <summary>
    /// Факты появляются только после закрытия регистрации. Сильные первые пароли не упоминаются.
    /// </summary>
    public async Task<IReadOnlyList<FactDto>> GetFactsAsync()
    {
        var ev = await db.Events.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Type == EventType.Registration && e.Status == EventStatus.Finished);
        if (ev is null)
        {
            return [];
        }

        var openedAt = ev.StartedAt!.Value;
        var closedAt = ev.FinishedAt!.Value;
        // Опоздавшие не участвовали в гонке — их время ничего не говорит.
        var inTime = (await db.RegistrationAttempts.AsNoTracking()
                .Where(a => a.CompletedAt != null)
                .ToListAsync())
            .Where(a => a.CompletedAt <= closedAt)
            .OrderBy(a => a.CompletedAt)
            .ToList();

        var facts = new List<FactDto>();
        if (inTime.Count > 0)
        {
            var fastest = inTime[0];
            facts.Add(new FactDto($"Быстрее всех зарегистрировался {fastest.Login} — {Format(fastest.CompletedAt!.Value - openedAt)}", [fastest.Login]));
        }

        if (inTime.Count > 1)
        {
            var slowest = inTime[^1];
            facts.Add(new FactDto($"Дольше всех ловил печеньки {slowest.Login} — {Format(slowest.CompletedAt!.Value - openedAt)}", [slowest.Login]));
        }

        foreach (var weak in inTime.Where(a => a.FirstMask is not null))
        {
            facts.Add(new FactDto($"Первый пароль {weak.Login}: {weak.FirstMask}", [weak.Login]));
            var patterns = JsonSerializer.Deserialize<List<string>>(weak.FirstPatternsJson) ?? [];
            facts.AddRange(patterns
                .Where(PatternPhrases.ContainsKey)
                .Select(p => new FactDto($"Первый пароль {weak.Login} {PatternPhrases[p]}", [weak.Login])));
        }

        return facts;
    }

    private static string Format(TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";
}
