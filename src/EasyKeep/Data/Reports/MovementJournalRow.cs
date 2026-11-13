namespace EasyKeep.Data.Reports;

/// <summary>Строка журнала движений для таблицы истории.</summary>
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
    /// <summary>Понятная подпись типа операции для интерфейса.</summary>
    public string TypeText => Type switch
    {
        "IN" => "Приход",
        "OUT" => "Расход",
        "TRANSFER" => "Перемещение",
        _ => Type
    };

    /// <summary>Маршрут документа: склад-источник и/или склад-получатель.</summary>
    public string RouteText => Type switch
    {
        "IN" => ToWarehouse ?? "—",
        "OUT" => FromWarehouse ?? "—",
        "TRANSFER" => $"{FromWarehouse ?? "—"} → {ToWarehouse ?? "—"}",
        _ => "—"
    };
}
