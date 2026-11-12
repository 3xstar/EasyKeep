namespace EasyKeep.Data.Reports;

/// <summary>Сводка остатков и стоимости по одному складу.</summary>
public record WarehouseStockRow(
    string WarehouseName,
    int ItemsCount,
    int TotalQuantity,
    decimal TotalValue);
