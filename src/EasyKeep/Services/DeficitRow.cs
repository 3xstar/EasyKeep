namespace EasyKeep.Services;

/// <summary>
/// Строка «Светофора дефицита» по конкретному товару на конкретном складе.
/// Подписи статусов задаются в XAML-триггерах из словаря локализации,
/// чтобы переключение языка происходило мгновенно.
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
    /// <summary>Прогноз в днях в виде текста; единица — по текущему языку.</summary>
    public string DaysLeftText => DaysLeft is null ? "—" : $"{DaysLeft:0.#} {LocalizationManager.Get("Common_DaysShort")}";
}
