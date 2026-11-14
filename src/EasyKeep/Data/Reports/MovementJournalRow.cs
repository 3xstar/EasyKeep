namespace EasyKeep.Data.Reports;

/// <summary>Строка журнала движений для таблицы истории.
/// Подпись типа задаётся в XAML-триггерах из словаря локализации.</summary>
public record MovementJournalRow(
    int Id,
    string Type,
    DateTime CreatedAt,
    string? FromWarehouse,
    string? ToWarehouse,
    string? Comment,
    int ItemsCount,
    int TotalQuantity)
{
    /// <summary>Маршрут документа: склад-источник и/или склад-получатель.</summary>
    public string RouteText => Type switch
    {
        "IN" => ToWarehouse ?? "—",
        "OUT" => FromWarehouse ?? "—",
        "TRANSFER" => $"{FromWarehouse ?? "—"} → {ToWarehouse ?? "—"}",
        _ => "—"
    };
}
