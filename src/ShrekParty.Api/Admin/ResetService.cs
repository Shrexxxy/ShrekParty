using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Admin;

/// <summary>
/// Сброс перед праздником: стирает игроков и результаты, сохраняет персонал и контент.
/// </summary>
public class ResetService(AppDbContext db, MediaStorage storage, ILiveNotifier live)
{
    /// <summary>
    /// Удаляет игроков, попытки регистрации, награды, ивенты и медиа игроков (вместе с файлами).
    /// </summary>
    public async Task ResetAsync()
    {
        var playerMedia = await db.MediaFiles.Where(m => m.UserId != null).ToListAsync();
        foreach (var media in playerMedia)
        {
            File.Delete(storage.PathFor(media));
        }

        db.MediaFiles.RemoveRange(playerMedia);
        await db.SaveChangesAsync();

        await db.DonkeyAwards.ExecuteDeleteAsync();
        await db.RegistrationAttempts.ExecuteDeleteAsync();
        await db.Events.ExecuteDeleteAsync();
        await db.Users.Where(u => u.Role == UserRole.Player).ExecuteDeleteAsync();

        await live.BroadcastAsync();
    }
}
