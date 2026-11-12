using EasyKeep.Data;
using EasyKeep.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyKeep.Services;

/// <summary>
/// «Авто-балансировка складов»: находит критичный дефицит на торговых точках
/// и формирует черновики перемещений с основного склада.
/// </summary>
public class AutoBalanceService
{
    private const string MainWarehouseName = "Основной склад";
    private const string DefectWarehouseKeyword = "брак";

    private readonly EasyKeepContext _context;
    private readonly DeficitService _deficitService;

    public AutoBalanceService(EasyKeepContext context)
    {
        _context = context;
        _deficitService = new DeficitService(context);
    }

    /// <summary>
    /// Возвращает черновики перемещений (по одному на торговую точку).
    /// Документы не проводятся — их должен подтвердить пользователь.
    /// </summary>
    public async Task<List<AutoBalanceDraft>> BuildDraftsAsync()
    {
        List<Warehouse> warehouses = await _context.Warehouses
            .AsNoTracking()
            .OrderBy(warehouse => warehouse.Id)
            .ToListAsync();

        Warehouse? mainWarehouse = FindMainWarehouse(warehouses);
        if (mainWarehouse is null)
            return new List<AutoBalanceDraft>();

        Dictionary<int, int> surplus = await LoadMainSurplusAsync(mainWarehouse.Id);
        List<DeficitRow> deficits = await _deficitService.GetDeficitRowsAsync();
        List<AutoBalanceDraft> drafts = new();

        foreach (Warehouse outlet in warehouses.Where(w => IsOutlet(w, mainWarehouse)))
        {
            List<MovementItem> lines = BuildLines(outlet, deficits, surplus);
            if (lines.Count == 0)
                continue;

            drafts.Add(new AutoBalanceDraft(
                outlet.Id,
                outlet.Name,
                new Movement
                {
                    Type = MovementTypes.Transfer,
                    FromWarehouseId = mainWarehouse.Id,
                    ToWarehouseId = outlet.Id,
                    CreatedAt = DateTime.Now,
                    Comment = $"Авто-балансировка: черновик для «{outlet.Name}»",
                    Items = lines
                }));
        }

        return drafts;
    }

    /// <summary>Основной склад ищем по названию, иначе берём первый в списке.</summary>
    private static Warehouse? FindMainWarehouse(List<Warehouse> warehouses)
    {
        return warehouses.FirstOrDefault(w =>
                   string.Equals(w.Name, MainWarehouseName, StringComparison.OrdinalIgnoreCase))
               ?? warehouses.FirstOrDefault();
    }

    private static bool IsOutlet(Warehouse warehouse, Warehouse mainWarehouse)
    {
        if (warehouse.Id == mainWarehouse.Id)
            return false;

        // Склад брака не участвует в балансировке товарных запасов.
        return !warehouse.Name.Contains(DefectWarehouseKeyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Излишек основного склада — количество сверх неснижаемого остатка.
    /// </summary>
    private async Task<Dictionary<int, int>> LoadMainSurplusAsync(int mainWarehouseId)
    {
        var rows = await _context.Stocks
            .AsNoTracking()
            .Where(stock => stock.WarehouseId == mainWarehouseId)
            .Select(stock => new
            {
                stock.ItemId,
                stock.Quantity,
                stock.Item!.MinStock
            })
            .ToListAsync();

        return rows.ToDictionary(
            row => row.ItemId,
            row => Math.Max(row.Quantity - row.MinStock, 0));
    }

    /// <summary>
    /// Формирует строки для одной торговой точки и сразу уменьшает
    /// доступный излишек, чтобы не распределить один товар дважды.
    /// </summary>
    private static List<MovementItem> BuildLines(
        Warehouse outlet,
        List<DeficitRow> deficits,
        Dictionary<int, int> surplus)
    {
        List<MovementItem> lines = new();

        foreach (DeficitRow row in deficits.Where(row =>
                     row.WarehouseId == outlet.Id && row.Status == DeficitStatus.Critical))
        {
            // Пополняем только до минимального порога.
            int needed = Math.Max(row.MinStock - row.Quantity, 0);
            if (needed == 0)
                continue;

            int available = surplus.GetValueOrDefault(row.ItemId);
            int quantity = Math.Min(needed, available);
            if (quantity == 0)
                continue;

            lines.Add(new MovementItem
            {
                ItemId = row.ItemId,
                Quantity = quantity
            });

            surplus[row.ItemId] = available - quantity;
        }

        return lines;
    }
}
