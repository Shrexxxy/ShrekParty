using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Events;

/// <summary>
/// Исход операции с программой вечера.
/// </summary>
public enum EngineOutcome
{
    Ok,
    NotFound,
    Unsupported,
    AlreadyActive,
    NotActive,
    NotDraft,
}

/// <summary>
/// Программа вечера: создание, порядок, старт, тик по времени, завершение с начислением осликов.
/// </summary>
public class EventEngine(
    AppDbContext db,
    IEnumerable<IEventModule> modules,
    DonkeyService donkeys,
    ILiveNotifier live,
    TimeProvider time)
{
    /// <summary>Типы, которые не входят в программу: живут своей жизнью.</summary>
    private static readonly EventType[] NotInProgram = [EventType.Registration, EventType.Tote];

    /// <summary>
    /// Максимум осликов по умолчанию для типа.
    /// </summary>
    public static int DefaultMaxDonkeys(EventType type) => type switch
    {
        EventType.Tournament or EventType.Laugh or EventType.HideAndSeek or EventType.Quiz => 100,
        EventType.PhotoHunt => 20,
        EventType.Reaction or EventType.Tapper or EventType.Days => 30,
        EventType.Mafia => 25,
        _ => 0,
    };

    /// <summary>
    /// Добавляет ивент в конец программы.
    /// </summary>
    /// <param name="parentId">Ивент, к которому привязан этот (аукцион боя — к бою).</param>
    public async Task<(EngineOutcome Outcome, ProgramItemDto? Item)> CreateAsync(CreateEventRequest request, Guid? parentId = null)
    {
        if (NotInProgram.Contains(request.Type) || ModuleFor(request.Type) is null)
        {
            return (EngineOutcome.Unsupported, null);
        }

        var order = (await db.Events.Where(e => !NotInProgram.Contains(e.Type)).MaxAsync(e => (int?)e.Order) ?? -1) + 1;
        if (parentId is { } p && await db.Events.FindAsync(p) is { } parent)
        {
            // Привязанный ивент — сразу за родителем: остальные сдвигаются на одно место.
            order = parent.Order + 1;
            await db.Events.Where(e => !NotInProgram.Contains(e.Type) && e.Order >= order)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Order, e => e.Order + 1));
        }

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Title = request.Title.Trim(),
            Status = EventStatus.Draft,
            Order = order,
            MaxDonkeys = request.MaxDonkeys ?? DefaultMaxDonkeys(request.Type),
            SettingsJson = request.Settings?.GetRawText(),
            ParentId = parentId,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return (EngineOutcome.Ok, ToDto(ev));
    }

    /// <summary>
    /// Программа вечера по порядку.
    /// </summary>
    public async Task<IReadOnlyList<ProgramItemDto>> GetProgramAsync() =>
        (await db.Events.AsNoTracking().Where(e => !NotInProgram.Contains(e.Type)).ToListAsync())
        .OrderBy(e => e.Order)
        .Select(ToDto)
        .ToList();

    /// <summary>
    /// Ставит ивент на позицию <paramref name="order"/>, остальные сдвигаются.
    /// </summary>
    public async Task<EngineOutcome> ReorderAsync(Guid id, int order)
    {
        var program = (await db.Events.Where(e => !NotInProgram.Contains(e.Type)).ToListAsync()).OrderBy(e => e.Order).ToList();
        var moved = program.SingleOrDefault(e => e.Id == id);
        if (moved is null)
        {
            return EngineOutcome.NotFound;
        }

        program.Remove(moved);
        program.Insert(Math.Clamp(order, 0, program.Count), moved);
        for (var i = 0; i < program.Count; i++)
        {
            program[i].Order = i;
        }

        await db.SaveChangesAsync();
        return EngineOutcome.Ok;
    }

    /// <summary>
    /// Удаляет ивент, который ещё не начинался.
    /// </summary>
    public async Task<EngineOutcome> DeleteAsync(Guid id)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null || NotInProgram.Contains(ev.Type))
        {
            return EngineOutcome.NotFound;
        }
        if (ev.Status != EventStatus.Draft)
        {
            return EngineOutcome.NotDraft;
        }

        db.Events.Remove(ev);
        await db.SaveChangesAsync();
        return EngineOutcome.Ok;
    }

    /// <summary>
    /// Запускает ивент. Пока идёт другой (включая открытую регистрацию) — нельзя.
    /// Правило модуля нарушено — <see cref="EventRuleException"/>, ничего не сохраняется.
    /// </summary>
    public async Task<EngineOutcome> StartAsync(Guid id)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null || NotInProgram.Contains(ev.Type))
        {
            return EngineOutcome.NotFound;
        }
        if (ev.Status != EventStatus.Draft)
        {
            return EngineOutcome.NotDraft;
        }
        if (await db.Events.AnyAsync(e => e.Status == EventStatus.Active && e.Type != EventType.Tote))
        {
            return EngineOutcome.AlreadyActive;
        }

        ev.Status = EventStatus.Active;
        ev.StartedAt = time.GetUtcNow();
        await ModuleFor(ev.Type)!.StartAsync(ev);
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
        return EngineOutcome.Ok;
    }

    /// <summary>
    /// Завершает ивент: места от модуля → ослики по схеме ивента.
    /// </summary>
    public async Task<EngineOutcome> FinishAsync(Guid id)
    {
        var ev = await db.Events.FindAsync(id);
        if (ev is null || NotInProgram.Contains(ev.Type))
        {
            return EngineOutcome.NotFound;
        }
        if (ev.Status != EventStatus.Active)
        {
            return EngineOutcome.NotActive;
        }

        var module = ModuleFor(ev.Type)!;
        var ranking = await module.RankAsync(ev);

        // Таймер и кнопка «Завершить» могут сработать одновременно — завершает только тот, кто первым сменил статус.
        var finishedAt = time.GetUtcNow();
        var claimed = await db.Events
            .Where(e => e.Id == id && e.Status == EventStatus.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EventStatus.Finished)
                .SetProperty(e => e.FinishedAt, finishedAt));
        if (claimed == 0)
        {
            return EngineOutcome.NotActive;
        }

        ev.Status = EventStatus.Finished;
        ev.FinishedAt = finishedAt;
        await db.SaveChangesAsync();

        if (ev.MaxDonkeys > 0)
        {
            await donkeys.AwardRankingAsync(ev.Id, ranking, ev.MaxDonkeys, ev.Title);
        }

        await module.AfterFinishAsync(ev);
        await db.SaveChangesAsync();

        // Начисление при пустом рейтинге ничего не рассылает, а экраны должны узнать, что ивент закончился.
        await live.BroadcastAsync();
        return EngineOutcome.Ok;
    }

    /// <summary>
    /// Тик таймеров текущего ивента; модуль решает, пора ли завершать.
    /// </summary>
    public async Task TickAsync()
    {
        var active = await ActiveEventAsync();
        if (active is null)
        {
            return;
        }

        var module = ModuleFor(active.Type)!;
        if (await module.TickAsync(active, time.GetUtcNow()))
        {
            await db.SaveChangesAsync();
            await FinishAsync(active.Id);
            return;
        }

        var changed = db.ChangeTracker.HasChanges();
        if (changed)
        {
            await db.SaveChangesAsync();
        }

        if (changed || module.BroadcastEveryTick(active))
        {
            await live.BroadcastAsync();
        }
    }

    /// <summary>
    /// Текущий ивент для снапшота — детали для конкретного зрителя.
    /// </summary>
    public async Task<ActiveEventDto?> GetActiveAsync(Guid? viewerId, bool isAdmin)
    {
        var active = await ActiveEventAsync();
        if (active is null)
        {
            return null;
        }

        var details = await ModuleFor(active.Type)!.GetDetailsAsync(active, viewerId, isAdmin);
        return new ActiveEventDto(active.Id, active.Type, active.Title, active.StartedAt, details);
    }

    private Task<Event?> ActiveEventAsync() =>
        db.Events.FirstOrDefaultAsync(e => e.Status == EventStatus.Active && !NotInProgram.Contains(e.Type));

    // Последний зарегистрированный модуль типа выигрывает — так тесты подменяют модуль.
    private IEventModule? ModuleFor(EventType type) => modules.LastOrDefault(m => m.Type == type);

    private static ProgramItemDto ToDto(Event e) => new(e.Id, e.Type, e.Title, e.Status, e.Order, e.MaxDonkeys);
}
