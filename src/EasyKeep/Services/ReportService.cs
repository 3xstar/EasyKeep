using EasyKeep.Data;
using EasyKeep.Data.Reports;

namespace EasyKeep.Services;

/// <summary>
/// Аналитические отчёты: сводка остатков по складам и рейтинг активных товаров.
/// </summary>
public class ReportService
{
    private readonly AdoNetMovementRepository _repository = new();

    public List<WarehouseStockRow> GetStockReport()
    {
        return _repository.GetStockReport();
    }

    public List<TopItemRow> GetTopItems(DateTime from, DateTime to, int limit = 10)
    {
        return _repository.GetTopItems(from, to, limit);
    }
}
