using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Quiz;

/// <summary>Вопрос в редакторе.</summary>
public record QuizQuestionDto(Guid Id, int Order, int Price, string Text, string Answer, string? MediaUrl, QuizMediaType? MediaType);

/// <summary>Тема в редакторе.</summary>
public record QuizThemeDto(Guid Id, int Round, int Order, string Name, IReadOnlyList<QuizQuestionDto> Questions);

/// <summary>Весь контент Своей игры.</summary>
public record QuizTreeDto(IReadOnlyList<QuizThemeDto> Themes);

/// <summary>Новая тема.</summary>
public record QuizThemeRequest(int Round, string? Name);

/// <summary>Переименование темы.</summary>
public record QuizThemeRenameRequest(string? Name);

/// <summary>Вопрос: текст, ответ и необязательная цена (по умолчанию — по раунду и номеру).</summary>
public record QuizQuestionRequest(string? Text, string? Answer, int? Price);

/// <summary>Вопрос в JSON импорта/экспорта.</summary>
public record QuizImportQuestion(string? Text, string? Answer, int? Price);

/// <summary>Тема в JSON импорта/экспорта.</summary>
public record QuizImportTheme(int Round, string? Name, IReadOnlyList<QuizImportQuestion>? Questions);

/// <summary>JSON импорта/экспорта (без медиа).</summary>
public record QuizImport(IReadOnlyList<QuizImportTheme>? Themes);

/// <summary>
/// Контент Своей игры: темы, вопросы, медиа, импорт/экспорт. Контент переживает «Сбросить всё».
/// </summary>
public class QuizContentService(AppDbContext db, MediaStorage media)
{
    /// <summary>Тег медиа вопросов — сброс их не трогает.</summary>
    public const string MediaTag = "quiz";

    /// <summary>Номер раунда финала.</summary>
    public const int FinalRound = 0;

    /// <summary>Шаг цены: 100 × раунд × номер вопроса в теме.</summary>
    public const int PriceStep = 100;

    /// <summary>Всё дерево: раунды по порядку, финал в конце.</summary>
    public async Task<QuizTreeDto> GetTreeAsync()
    {
        var themes = await db.QuizThemes.AsNoTracking().ToListAsync();
        var questions = await db.QuizQuestions.AsNoTracking().ToListAsync();
        return new QuizTreeDto(themes
            .OrderBy(t => t.Round == FinalRound)
            .ThenBy(t => t.Round)
            .ThenBy(t => t.Order)
            .Select(t => new QuizThemeDto(t.Id, t.Round, t.Order, t.Name, questions
                .Where(q => q.ThemeId == t.Id)
                .OrderBy(q => q.Order)
                .Select(q => new QuizQuestionDto(q.Id, q.Order, q.Price, q.Text, q.Answer, q.MediaId is { } m ? MediaStorage.UrlFor(m) : null, q.MediaType))
                .ToList()))
            .ToList());
    }

    /// <summary>Новая тема в конец раунда; второй финал — <c>final_exists</c>.</summary>
    public async Task<Guid> AddThemeAsync(QuizThemeRequest request)
    {
        await EnsureNotInGameAsync();
        var name = Required(request.Name, "bad_name");
        if (request.Round < FinalRound)
        {
            throw new BadInputException("bad_round");
        }
        if (request.Round == FinalRound && await db.QuizThemes.AnyAsync(t => t.Round == FinalRound))
        {
            throw new EventRuleException("final_exists");
        }

        var order = await db.QuizThemes.CountAsync(t => t.Round == request.Round);
        var theme = new QuizTheme { Id = Guid.NewGuid(), Round = request.Round, Order = order, Name = name };
        db.QuizThemes.Add(theme);
        await db.SaveChangesAsync();
        return theme.Id;
    }

    /// <summary>Переименовать тему.</summary>
    public async Task RenameThemeAsync(Guid themeId, QuizThemeRenameRequest request)
    {
        await EnsureNotInGameAsync();
        var theme = await db.QuizThemes.FirstOrDefaultAsync(t => t.Id == themeId) ?? throw new KeyNotFoundException();
        theme.Name = Required(request.Name, "bad_name");
        await db.SaveChangesAsync();
    }

    /// <summary>Удалить тему со всеми вопросами и их медиа.</summary>
    public async Task DeleteThemeAsync(Guid themeId)
    {
        await EnsureNotInGameAsync();
        var theme = await db.QuizThemes.FirstOrDefaultAsync(t => t.Id == themeId) ?? throw new KeyNotFoundException();
        var questions = await db.QuizQuestions.Where(q => q.ThemeId == themeId).ToListAsync();
        foreach (var question in questions)
        {
            await RemoveMediaFileAsync(question);
        }

        db.QuizQuestions.RemoveRange(questions);
        db.QuizThemes.Remove(theme);
        await db.SaveChangesAsync();
    }

    /// <summary>Вопрос в конец темы; во второй вопрос финала — <c>final_full</c>.</summary>
    public async Task<Guid> AddQuestionAsync(Guid themeId, QuizQuestionRequest request)
    {
        await EnsureNotInGameAsync();
        var theme = await db.QuizThemes.FirstOrDefaultAsync(t => t.Id == themeId) ?? throw new KeyNotFoundException();
        var (text, answer) = (Required(request.Text, "bad_text"), Required(request.Answer, "bad_answer"));
        var order = await db.QuizQuestions.CountAsync(q => q.ThemeId == themeId);
        if (theme.Round == FinalRound && order > 0)
        {
            throw new EventRuleException("final_full");
        }

        var question = new QuizQuestion
        {
            Id = Guid.NewGuid(),
            ThemeId = themeId,
            Order = order,
            Price = Price(theme.Round, order, request.Price),
            Text = text,
            Answer = answer,
        };
        db.QuizQuestions.Add(question);
        await db.SaveChangesAsync();
        return question.Id;
    }

    /// <summary>Изменить текст, ответ и цену.</summary>
    public async Task UpdateQuestionAsync(Guid questionId, QuizQuestionRequest request)
    {
        await EnsureNotInGameAsync();
        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId) ?? throw new KeyNotFoundException();
        var theme = await db.QuizThemes.FirstAsync(t => t.Id == question.ThemeId);
        question.Text = Required(request.Text, "bad_text");
        question.Answer = Required(request.Answer, "bad_answer");
        question.Price = Price(theme.Round, question.Order, request.Price);
        await db.SaveChangesAsync();
    }

    /// <summary>Удалить вопрос с медиа.</summary>
    public async Task DeleteQuestionAsync(Guid questionId)
    {
        await EnsureNotInGameAsync();
        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId) ?? throw new KeyNotFoundException();
        await RemoveMediaFileAsync(question);
        db.QuizQuestions.Remove(question);
        await db.SaveChangesAsync();
    }

    /// <summary>Прикрепить картинку, аудио или видео (заменяет прежнее).</summary>
    public async Task SetMediaAsync(Guid questionId, Stream content, string contentType, Guid adminId)
    {
        await EnsureNotInGameAsync();
        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId) ?? throw new KeyNotFoundException();
        var (type, kind) = contentType.Split('/')[0].ToLowerInvariant() switch
        {
            "image" => (QuizMediaType.Image, MediaKind.Photo),
            "audio" => (QuizMediaType.Audio, MediaKind.Audio),
            "video" => (QuizMediaType.Video, MediaKind.Video),
            _ => throw new BadInputException("bad_file"),
        };

        await RemoveMediaFileAsync(question);
        var file = await media.SaveAsync(content, contentType, kind, adminId, tag: MediaTag);
        question.MediaId = file.Id;
        question.MediaType = type;
        await db.SaveChangesAsync();
    }

    /// <summary>Убрать медиа у вопроса.</summary>
    public async Task RemoveMediaAsync(Guid questionId)
    {
        await EnsureNotInGameAsync();
        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId) ?? throw new KeyNotFoundException();
        await RemoveMediaFileAsync(question);
        await db.SaveChangesAsync();
    }

    /// <summary>JSON для правки снаружи (без медиа).</summary>
    public async Task<QuizImport> ExportAsync() =>
        new((await GetTreeAsync()).Themes
            .Select(t => new QuizImportTheme(t.Round, t.Name, t.Questions.Select(q => new QuizImportQuestion(q.Text, q.Answer, q.Price)).ToList()))
            .ToList());

    /// <summary>Заменить весь контент; медиа прежних вопросов удаляются.</summary>
    public async Task ImportAsync(QuizImport import)
    {
        await EnsureNotInGameAsync();
        var themes = import.Themes ?? [];
        var finals = themes.Where(t => t.Round == FinalRound).ToList();
        if (themes.Any(t => t.Round < FinalRound) || finals.Count > 1 || finals.Any(f => f.Questions?.Count != 1))
        {
            throw new BadInputException("bad_final");
        }

        // Проверяем всё до удаления старого — кривой файл не должен оставить пустую игру.
        var prepared = themes.Select(t => (
            Theme: t,
            Name: Required(t.Name, "bad_name"),
            Questions: (t.Questions ?? []).Select(q => (Text: Required(q.Text, "bad_text"), Answer: Required(q.Answer, "bad_answer"), q.Price)).ToList()))
            .ToList();

        // Всё в базе — одной транзакцией; файлы с диска — только после неё, иначе сбой оставил бы вопросы без медиа.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var mediaIds = await db.QuizQuestions.Where(q => q.MediaId != null).Select(q => q.MediaId!.Value).ToListAsync();
        var oldFiles = await db.MediaFiles.Where(m => mediaIds.Contains(m.Id)).ToListAsync();
        db.MediaFiles.RemoveRange(oldFiles);
        await db.SaveChangesAsync();
        await db.QuizQuestions.ExecuteDeleteAsync();
        await db.QuizThemes.ExecuteDeleteAsync();

        foreach (var group in prepared.GroupBy(p => p.Theme.Round))
        {
            var themeOrder = 0;
            foreach (var (theme, name, questions) in group)
            {
                var entity = new QuizTheme { Id = Guid.NewGuid(), Round = theme.Round, Order = themeOrder++, Name = name };
                db.QuizThemes.Add(entity);
                db.QuizQuestions.AddRange(questions.Select((q, i) => new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    ThemeId = entity.Id,
                    Order = i,
                    Price = Price(theme.Round, i, q.Price),
                    Text = q.Text,
                    Answer = q.Answer,
                }));
            }
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        foreach (var file in oldFiles)
        {
            File.Delete(media.PathFor(file));
        }
    }

    private static int Price(int round, int order, int? explicitPrice) =>
        round == FinalRound ? 0 : explicitPrice is > 0 ? explicitPrice.Value : PriceStep * round * (order + 1);

    private static string Required(string? value, string code) =>
        string.IsNullOrWhiteSpace(value) ? throw new BadInputException(code) : value.Trim();

    private async Task EnsureNotInGameAsync()
    {
        if (await db.Events.AnyAsync(e => e.Type == EventType.Quiz && e.Status == EventStatus.Active))
        {
            throw new EventRuleException("in_game");
        }
    }

    private async Task RemoveMediaFileAsync(QuizQuestion question)
    {
        if (question.MediaId is { } mediaId && await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == mediaId) is { } file)
        {
            File.Delete(media.PathFor(file));
            db.MediaFiles.Remove(file);
        }

        question.MediaId = null;
        question.MediaType = null;
    }
}
