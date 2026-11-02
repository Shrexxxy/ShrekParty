using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.HideAndSeek;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;
using ShrekParty.Api.Neuro;

namespace ShrekParty.Api.Admin;

/// <summary>
/// Сброс перед праздником: стирает игроков и результаты, сохраняет персонал и контент.
/// </summary>
public class ResetService(AppDbContext db, MediaStorage storage, ILiveNotifier live)
{
    /// <summary>
    /// Удаляет игроков, попытки регистрации, награды и медиа игроков (вместе с файлами).
    /// Программа вечера остаётся, но снова черновиком; регистрация и тотализаторы удаляются.
    /// </summary>
    public async Task ResetAsync()
    {
        // Медиа игроков и всё, что наснимали на репетиции (фото матчей, прятки); мемы и контент админа остаются.
        var playerMedia = await db.MediaFiles.Where(m => m.UserId != null || m.MatchId != null || m.Tag == HideSeekModule.MediaTag).ToListAsync();
        foreach (var media in playerMedia)
        {
            File.Delete(storage.PathFor(media));
        }

        db.MediaFiles.RemoveRange(playerMedia);
        await db.SaveChangesAsync();

        await db.DonkeyAwards.ExecuteDeleteAsync();
        await db.Bets.ExecuteDeleteAsync();
        await db.LaughScores.ExecuteDeleteAsync();
        await db.HideVotes.ExecuteDeleteAsync();
        await db.HideShotPlayers.ExecuteDeleteAsync();
        await db.HideShots.ExecuteDeleteAsync();
        await db.PhotoSubmissions.ExecuteDeleteAsync();
        await db.PhotoLikes.ExecuteDeleteAsync();
        await db.ReactionAttempts.ExecuteDeleteAsync();
        await db.TapScores.ExecuteDeleteAsync();
        await db.DaysAnswers.ExecuteDeleteAsync();
        await db.NeuroGuesses.ExecuteDeleteAsync();
        await db.QuizPresses.ExecuteDeleteAsync();
        await db.QuizScoreEntries.ExecuteDeleteAsync();
        await db.QuizFinalAnswers.ExecuteDeleteAsync();
        await db.AuctionSales.ExecuteDeleteAsync();
        await db.Pets.ExecuteDeleteAsync();
        await db.TvMessages.ExecuteDeleteAsync();
        await db.TournamentMatches.ExecuteDeleteAsync();
        await db.RegistrationAttempts.ExecuteDeleteAsync();
        await db.Events.Where(e => e.Type == EventType.Registration || e.Type == EventType.Tote).ExecuteDeleteAsync();
        await db.Events.ExecuteUpdateAsync(e => e
            .SetProperty(x => x.Status, EventStatus.Draft)
            .SetProperty(x => x.StartedAt, (DateTimeOffset?)null)
            .SetProperty(x => x.FinishedAt, (DateTimeOffset?)null)
            .SetProperty(x => x.StateJson, (string?)null)
            .SetProperty(x => x.ResultJson, (string?)null));
        await db.Users.Where(u => u.Role == UserRole.Player).ExecuteDeleteAsync();

        await live.BroadcastAsync();
    }
}
