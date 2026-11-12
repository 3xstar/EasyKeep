namespace EasyKeep.Services;

/// <summary>
/// Строка «Светофора дефицита» по конкретному товару на конкретном складе.
/// </summary>
public record DeficitRow(
    int WarehouseId,
    string WarehouseName,
    int ItemId,
    string Sku,
    string ItemName,
    int Quantity,
    int MinStock,
    double AverageDailyConsumption,
    decimal? DaysLeft,
    DeficitStatus Status)
{
    /// <summary>Подпись статуса с цветовым маркером для интерфейса.</summary>
    public string StatusText => Status switch
    {
        DeficitStatus.Normal => "🟢 Норма",
        DeficitStatus.Warning => "🟡 Внимание",
        DeficitStatus.Critical => "🔴 Критично",
        DeficitStatus.NonLiquid => "⚪ Неликвид",
        _ => "—"
    };

    /// <summary>Прогноз в днях в виде текста.</summary>
    public string DaysLeftText => DaysLeft is null ? "—" : $"{DaysLeft:0.#} дн.";
}
