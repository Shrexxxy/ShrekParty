using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Media;
using ShrekParty.Api.PhotoHunt;
using ShrekParty.Api.Tournament;

namespace ShrekParty.Api.Tv;

/// <summary>Вид слайда карусели.</summary>
public enum SlideKind
{
    Fact,
    Photo,
}

/// <summary>Слайд карусели ТВ; логины — чтобы подсветить ники.</summary>
public record SlideDto(SlideKind Kind, string Text, IReadOnlyList<string> Logins, string? ImageUrl);

/// <summary>
/// Слайды карусели между ивентами: факты регистрации, фото матчей с подписью по шаблону, мемы, фото фотоохот и пряток — вперемешку.
/// </summary>
public class TvSlidesService(AppDbContext db, TvFactsService facts, Random random)
{
    /// <summary>
    /// Все слайды в случайном порядке.
    /// </summary>
    public async Task<IReadOnlyList<SlideDto>> GetSlidesAsync()
    {
        var slides = new List<SlideDto>();
        slides.AddRange((await facts.GetFactsAsync()).Select(f => new SlideDto(SlideKind.Fact, f.Text, f.Logins, null)));
        slides.AddRange(await MatchPhotosAsync());
        slides.AddRange(await MemesAsync());
        slides.AddRange(await HuntPhotosAsync());
        slides.AddRange(await HideShotsAsync());

        var shuffled = slides.ToArray();
        random.Shuffle(shuffled);
        return shuffled;
    }

    private async Task<IEnumerable<SlideDto>> MatchPhotosAsync()
    {
        var photos = await db.MediaFiles.AsNoTracking().Where(m => m.MatchId != null).ToListAsync();
        if (photos.Count == 0)
        {
            return [];
        }

        var matchIds = photos.Select(p => p.MatchId!.Value).Distinct().ToList();
        var matches = await db.TournamentMatches.AsNoTracking().Where(m => matchIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        var userIds = matches.Values.SelectMany(m => new[] { m.PlayerAId, m.PlayerBId }).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var captions = await db.CaptionTemplates.AsNoTracking().Select(c => c.Text).ToListAsync();

        string Name(Guid? id) => id is { } x && users.TryGetValue(x, out var u)
            ? u.Role == UserRole.Admin ? TournamentModule.AdminDisplayName : u.Login
            : "???";

        return photos
            .Where(p => matches.ContainsKey(p.MatchId!.Value))
            .Select(p =>
            {
                var match = matches[p.MatchId!.Value];
                var (a, b) = (Name(match.PlayerAId), Name(match.PlayerBId));
                var template = captions.Count > 0 ? captions[random.Next(captions.Count)] : "{A} И {B}";
                return new SlideDto(SlideKind.Photo, template.Replace("{A}", a).Replace("{B}", b), [a, b], MediaStorage.UrlFor(p.Id));
            });
    }

    private async Task<IEnumerable<SlideDto>> MemesAsync() =>
        (await db.MediaFiles.AsNoTracking().Where(m => m.Tag == TvContentService.MemeTag).ToListAsync())
        .Select(m => new SlideDto(SlideKind.Photo, m.Caption ?? "", [], MediaStorage.UrlFor(m.Id)));

    private async Task<IEnumerable<SlideDto>> HideShotsAsync()
    {
        var shots = await db.HideShots.AsNoTracking().ToListAsync();
        if (shots.Count == 0)
        {
            return [];
        }

        var marks = await db.HideShotPlayers.AsNoTracking().ToListAsync();
        var logins = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Login);

        return shots.Select(s =>
        {
            var caught = marks.Where(m => m.ShotId == s.Id && logins.ContainsKey(m.UserId)).Select(m => logins[m.UserId]).ToList();
            var text = caught.Count > 0 ? $"Прятки: попались {string.Join(", ", caught)}" : "Прятки: тут никого… или нет? 👀";
            return new SlideDto(SlideKind.Photo, text, caught, MediaStorage.UrlFor(s.MediaId));
        });
    }

    private async Task<IEnumerable<SlideDto>> HuntPhotosAsync()
    {
        var submissions = await db.PhotoSubmissions.AsNoTracking().ToListAsync();
        if (submissions.Count == 0)
        {
            return [];
        }

        var eventIds = submissions.Select(s => s.EventId).Distinct().ToList();
        var events = await db.Events.AsNoTracking().Where(e => eventIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        var userIds = submissions.Select(s => s.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);

        return submissions
            .Where(s => users.ContainsKey(s.UserId) && events.TryGetValue(s.EventId, out var ev) && ev.StateJson is not null)
            .Select(s =>
            {
                var task = PhotoHuntModule.StateOf(events[s.EventId]).Task;
                var login = users[s.UserId].Login;
                return new SlideDto(SlideKind.Photo, $"Фотоохота «{task}»: {login}", [login], MediaStorage.UrlFor(s.MediaId));
            });
    }
}
