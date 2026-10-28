using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Auction;

/// <summary>Лот в админке.</summary>
public record AuctionLotDto(Guid Id, int Order, string Name, int StartPrice, string? PhotoUrl);

/// <summary>Новый лот или правка.</summary>
public record AuctionLotRequest(string? Name, int StartPrice);

/// <summary>
/// Лоты аукциона: заводятся заранее, порядок — порядок добавления.
/// </summary>
public class AuctionLotService(AppDbContext db, MediaStorage media, TimeProvider time)
{
    /// <summary>Тег медиа лотов — сброс их не трогает.</summary>
    public const string MediaTag = "auction";

    /// <summary>Все лоты по порядку.</summary>
    public async Task<IReadOnlyList<AuctionLotDto>> GetLotsAsync() =>
        (await db.AuctionLots.AsNoTracking().ToListAsync())
        .OrderBy(l => l.Order)
        .Select(ToDto)
        .ToList();

    /// <summary>Новый лот в конец.</summary>
    public async Task<Guid> AddAsync(AuctionLotRequest request)
    {
        await EnsureNotInAuctionAsync();
        var (name, price) = Validate(request);
        var order = await db.AuctionLots.CountAsync() == 0 ? 0 : await db.AuctionLots.MaxAsync(l => l.Order) + 1;
        var lot = new AuctionLot { Id = Guid.NewGuid(), Order = order, Name = name, StartPrice = price, CreatedAt = time.GetUtcNow() };
        db.AuctionLots.Add(lot);
        await db.SaveChangesAsync();
        return lot.Id;
    }

    /// <summary>Изменить название и стартовую цену.</summary>
    public async Task UpdateAsync(Guid lotId, AuctionLotRequest request)
    {
        await EnsureNotInAuctionAsync();
        var lot = await db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId) ?? throw new KeyNotFoundException();
        (lot.Name, lot.StartPrice) = Validate(request);
        await db.SaveChangesAsync();
    }

    /// <summary>Удалить лот с фото; проданный — нельзя (иначе покупка пропадёт из истории, а ослики останутся списаны).</summary>
    public async Task DeleteAsync(Guid lotId)
    {
        await EnsureNotInAuctionAsync();
        var lot = await db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId) ?? throw new KeyNotFoundException();
        if (await db.AuctionSales.AnyAsync(s => s.LotId == lotId))
        {
            throw new EventRuleException("lot_sold");
        }

        await RemovePhotoFileAsync(lot);
        db.AuctionLots.Remove(lot);
        await db.SaveChangesAsync();
    }

    /// <summary>Фото лота (заменяет прежнее).</summary>
    public async Task SetPhotoAsync(Guid lotId, Stream content, string contentType, Guid adminId)
    {
        await EnsureNotInAuctionAsync();
        var lot = await db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId) ?? throw new KeyNotFoundException();
        await RemovePhotoFileAsync(lot);
        var file = await media.SaveAsync(content, contentType, MediaKind.Photo, adminId, tag: MediaTag);
        lot.MediaId = file.Id;
        await db.SaveChangesAsync();
    }

    /// <summary>Убрать фото.</summary>
    public async Task RemovePhotoAsync(Guid lotId)
    {
        await EnsureNotInAuctionAsync();
        var lot = await db.AuctionLots.FirstOrDefaultAsync(l => l.Id == lotId) ?? throw new KeyNotFoundException();
        await RemovePhotoFileAsync(lot);
        await db.SaveChangesAsync();
    }

    internal static AuctionLotDto ToDto(AuctionLot lot) =>
        new(lot.Id, lot.Order, lot.Name, lot.StartPrice, lot.MediaId is { } m ? MediaStorage.UrlFor(m) : null);

    private static (string Name, int StartPrice) Validate(AuctionLotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadInputException("bad_name");
        }
        if (request.StartPrice < 1)
        {
            throw new BadInputException("bad_price");
        }

        return (request.Name.Trim(), request.StartPrice);
    }

    private async Task EnsureNotInAuctionAsync()
    {
        if (await db.Events.AnyAsync(e => e.Type == EventType.Auction && e.Status == EventStatus.Active))
        {
            throw new EventRuleException("in_game");
        }
    }

    private async Task RemovePhotoFileAsync(AuctionLot lot)
    {
        if (lot.MediaId is { } mediaId && await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == mediaId) is { } file)
        {
            File.Delete(media.PathFor(file));
            db.MediaFiles.Remove(file);
        }

        lot.MediaId = null;
    }
}

/// <summary>
/// Маршруты лотов в админке.
/// </summary>
public static class AuctionLotEndpoints
{
    /// <summary>
    /// <c>/api/admin/auction/lots</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapAuctionLots(this IEndpointRouteBuilder app)
    {
        var lots = app.WithRuleErrors().MapGroup("/api/admin/auction/lots").RequireAuthorization(AuthEndpoints.AdminPolicy);

        lots.MapGet("/", (AuctionLotService service) => service.GetLotsAsync());

        lots.MapPost("/", async (AuctionLotRequest request, AuctionLotService service) =>
        {
            var id = await service.AddAsync(request);
            return Results.Created($"/api/admin/auction/lots/{id}", new { id });
        });

        lots.MapPut("/{id:guid}", async (Guid id, AuctionLotRequest request, AuctionLotService service) =>
        {
            await service.UpdateAsync(id, request);
            return Results.NoContent();
        });

        lots.MapDelete("/{id:guid}", async (Guid id, AuctionLotService service) =>
        {
            await service.DeleteAsync(id);
            return Results.NoContent();
        });

        lots.MapPost("/{id:guid}/photo", async (Guid id, IFormFile? file, ClaimsPrincipal user, AuctionLotService service) =>
        {
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            await service.SetPhotoAsync(id, stream, file.ContentType, user.UserId()!.Value);
            return Results.NoContent();
        }).DisableAntiforgery();

        lots.MapDelete("/{id:guid}/photo", async (Guid id, AuctionLotService service) =>
        {
            await service.RemovePhotoAsync(id);
            return Results.NoContent();
        });

        return app;
    }
}
