using EasyKeep.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Data.Reports;
using Microsoft.EntityFrameworkCore;

namespace EasyKeep.Services;

/// <summary>
/// Проверка остатков и проведение документов движения.
/// Финальную проверку под блокировкой выполняет AdoNetMovementRepository.
/// </summary>
public class MovementService
{
    private readonly EasyKeepContext _context;
    private readonly AdoNetMovementRepository _repository;

    public MovementService(EasyKeepContext context)
    {
        _context = context;
        _repository = new AdoNetMovementRepository();
    }

    /// <summary>
    /// Проверяет, хватает ли остатка на складе-отправителе.
    /// Возвращает список нехваток с доступным максимумом для подсказки.
    /// </summary>
    public async Task<StockCheckResult> CheckStockAsync(Movement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        if (movement.Items.Count == 0)
            throw new InvalidOperationException("Накладная не содержит товарных позиций.");

        // Для прихода остаток не списывается, проверять нечего.
        if (movement.Type == MovementTypes.In)
            return new StockCheckResult(true, Array.Empty<StockShortage>());

        if (movement.FromWarehouseId is null)
            throw new InvalidOperationException("Не указан склад-отправитель.");

        int warehouseId = movement.FromWarehouseId.Value;
        List<StockShortage> shortages = new();

        foreach (MovementItem line in movement.Items)
        {
            int available = await GetAvailableQuantityAsync(warehouseId, line.ItemId);
            if (available >= line.Quantity)
                continue;

            var item = await _context.Items
                .AsNoTracking()
                .Where(item => item.Id == line.ItemId)
                .Select(item => new { item.Sku, item.Name })
                .FirstOrDefaultAsync();

            shortages.Add(new StockShortage(
                item?.Sku ?? "—",
                item?.Name ?? $"Товар #{line.ItemId}",
                available,
                line.Quantity));
        }

        return new StockCheckResult(shortages.Count == 0, shortages);
    }

    /// <summary>
    /// Проверяет остатки и проводит документ через ADO.NET.
    /// При нехватке бросает InsufficientStockException с доступным максимумом.
    /// </summary>
    public async Task<int> PostMovementAsync(Movement movement)
    {
        StockCheckResult check = await CheckStockAsync(movement);

        if (!check.IsEnough)
        {
            StockShortage firstShortage = check.Shortages[0];
            throw new InsufficientStockException(
                firstShortage.ItemName,
                firstShortage.Available,
                firstShortage.Requested);
        }

        return _repository.CreateMovement(movement);
    }

    /// <summary>Доступный остаток товара на складе (0, если строки ещё нет).</summary>
    public async Task<int> GetAvailableQuantityAsync(int warehouseId, int itemId)
    {
        return await _context.Stocks
            .AsNoTracking()
            .Where(stock => stock.WarehouseId == warehouseId && stock.ItemId == itemId)
            .Select(stock => stock.Quantity)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Ищет товар для «Командной строки склада»: сначала точный SKU,
    /// затем частичное совпадение по названию.
    /// </summary>
    public async Task<Item?> FindItemAsync(string query)
    {
        string value = query.Trim();
        if (value.Length == 0)
            return null;

        Item? bySku = await _context.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Sku == value);
        if (bySku is not null)
            return bySku;

        return await _context.Items
            .AsNoTracking()
            .Where(item => item.Name.Contains(value))
            .OrderBy(item => item.Name)
            .FirstOrDefaultAsync();
    }

    /// <summary>Товары по списку id — для подписей в предпросмотре авто-балансировки.</summary>
    public async Task<List<Item>> GetItemsAsync(IEnumerable<int> itemIds)
    {
        int[] ids = itemIds.Distinct().ToArray();
        return await _context.Items
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToListAsync();
    }

    /// <summary>Журнал движений (делегируется в ADO.NET репозиторий).</summary>
    public List<MovementJournalRow> GetJournal(
        DateTime from,
        DateTime to,
        string? type = null,
        int? warehouseId = null,
        string? search = null)
    {
        return _repository.GetJournal(from, to, type, warehouseId, search);
    }
}
