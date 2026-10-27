using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Tests.Events;

/// <summary>
/// Модуль-заглушка вместо Смехлыста: места по алфавиту логинов, детали — кто смотрит.
/// </summary>
public class FakeLaughModule(AppDbContext db) : IEventModule
{
    /// <summary>Сколько раз вызывали тик — для тестов таймера.</summary>
    public static int Ticks;

    /// <summary>Если задан — подсчёт мест ждёт, пока в него войдут все участники (гонка двух завершений).</summary>
    public static Barrier? RankBarrier;

    public EventType Type => EventType.Laugh;

    public Task StartAsync(Event ev) => Task.CompletedTask;

    public Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin) =>
        Task.FromResult<object?>(new { fake = true, viewerId, isAdmin });

    public Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        Interlocked.Increment(ref Ticks);
        return Task.FromResult(false);
    }

    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var players = await db.Users.Where(u => u.Role == UserRole.Player).ToListAsync();
        if (RankBarrier is { } barrier)
        {
            await Task.Run(() => barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
        }

        return DenseRanking.Rank(players, p => p.Login, descending: false)
            .Select(r => new Ranked<Guid>(r.Item.Id, r.Place))
            .ToList();
    }
}
