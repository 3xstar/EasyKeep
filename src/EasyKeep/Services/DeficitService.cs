using EasyKeep.Data;
using EasyKeep.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyKeep.Services;

/// <summary>
/// «Светофор дефицита»: средний расход за 30 дней, прогноз дней до нуля
/// и статус товара на каждом складе.
/// </summary>
public class DeficitService
{
    private const int AnalysisDays = 30;
    private const decimal CriticalDays = 2m;
    private const decimal WarningDays = 10m;

    private readonly EasyKeepContext _context;

    public DeficitService(EasyKeepContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Возвращает строки светофора по всем складам или только по выбранному.
    /// Сначала идут критичные позиции.
    /// </summary>
    public async Task<List<DeficitRow>> GetDeficitRowsAsync(int? warehouseId = null)
    {
        List<StockSnapshot> stocks = await LoadStocksAsync(warehouseId);
        Dictionary<(int WarehouseId, int ItemId), int> consumption = await LoadConsumptionAsync(warehouseId);

        return stocks
            .Select(stock => BuildRow(stock, consumption.GetValueOrDefault((stock.WarehouseId, stock.ItemId))))
            .OrderBy(row => row.Status)
            .ThenBy(row => row.WarehouseName)
            .ThenBy(row => row.ItemName)
            .ToList();
    }

    private async Task<List<StockSnapshot>> LoadStocksAsync(int? warehouseId)
    {
        IQueryable<Stock> query = _context.Stocks.AsNoTracking();

        if (warehouseId is > 0)
            query = query.Where(stock => stock.WarehouseId == warehouseId.Value);

        return await query
            .Select(stock => new StockSnapshot(
                stock.WarehouseId,
                stock.Warehouse!.Name,
                stock.ItemId,
                stock.Item!.Sku,
                stock.Item.Name,
                stock.Quantity,
                stock.Item.MinStock))
            .ToListAsync();
    }

    /// <summary>
    /// Суммирует расход со склада за последние 30 дней по паре «склад — товар».
    /// Расходом считаются продажи/списания (OUT) и исходящие перемещения (TRANSFER).
    /// </summary>
    private async Task<Dictionary<(int WarehouseId, int ItemId), int>> LoadConsumptionAsync(int? warehouseId)
    {
        DateTime since = DateTime.Today.AddDays(-AnalysisDays);

        IQueryable<MovementItem> query = _context.MovementItems
            .AsNoTracking()
            .Where(line => (line.Movement!.Type == MovementTypes.Out
                            || line.Movement.Type == MovementTypes.Transfer)
                           && line.Movement.CreatedAt >= since
                           && line.Movement.FromWarehouseId != null);

        if (warehouseId is > 0)
            query = query.Where(line => line.Movement!.FromWarehouseId == warehouseId.Value);

        var totals = await query
            .GroupBy(line => new { WarehouseId = line.Movement!.FromWarehouseId!.Value, line.ItemId })
            .Select(group => new
            {
                group.Key.WarehouseId,
                group.Key.ItemId,
                Total = group.Sum(line => line.Quantity)
            })
            .ToListAsync();

        return totals.ToDictionary(row => (row.WarehouseId, row.ItemId), row => row.Total);
    }

    private static DeficitRow BuildRow(StockSnapshot stock, int totalConsumption)
    {
        double averageDaily = Math.Round(totalConsumption / (double)AnalysisDays, 2);

        // Если расхода нет, прогноз построить нельзя — товар считается неликвидом.
        decimal? daysLeft = averageDaily > 0
            ? Math.Round((decimal)(stock.Quantity / averageDaily), 1)
            : null;

        DeficitStatus status = DetermineStatus(stock, totalConsumption, daysLeft);

        return new DeficitRow(
            stock.WarehouseId,
            stock.WarehouseName,
            stock.ItemId,
            stock.Sku,
            stock.ItemName,
            stock.Quantity,
            stock.MinStock,
            averageDaily,
            daysLeft,
            status);
    }

    private static DeficitStatus DetermineStatus(
        StockSnapshot stock,
        int totalConsumption,
        decimal? daysLeft)
    {
        // ⚪ Нет продаж за 30 дней — товар залежался.
        if (totalConsumption == 0)
            return DeficitStatus.NonLiquid;

        // 🔴 Остаток ниже порога или закончится в ближайшие два дня.
        if (stock.Quantity < stock.MinStock || daysLeft <= CriticalDays)
            return DeficitStatus.Critical;

        // 🟡 Запаса хватит примерно на 3–10 дней.
        if (daysLeft <= WarningDays)
            return DeficitStatus.Warning;

        // 🟢 Запаса больше чем на 10 дней.
        return DeficitStatus.Normal;
    }

    private record StockSnapshot(
        int WarehouseId,
        string WarehouseName,
        int ItemId,
        string Sku,
        string ItemName,
        int Quantity,
        int MinStock);
}
