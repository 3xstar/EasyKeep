namespace EasyKeep.Data.Reports;

/// <summary>Одно событие в истории выбранного товара («Рентген товара»).
/// Подпись типа задаётся в XAML-триггерах из словаря локализации.</summary>
public record ItemMovementRow(
    int MovementId,
    DateTime CreatedAt,
    string Type,
    string? FromWarehouse,
    string? ToWarehouse,
    int Quantity,
    string? Comment)
{
    public string DateText => CreatedAt.ToString("dd.MM.yyyy HH:mm");

    public string RouteText => Type switch
    {
        "IN" => $"→ {ToWarehouse ?? "—"}",
        "OUT" => $"← {FromWarehouse ?? "—"}",
        "TRANSFER" => $"{FromWarehouse ?? "—"} → {ToWarehouse ?? "—"}",
        _ => "—"
    };

    public string QuantityText => Type switch
    {
        "IN" => $"+{Quantity}",
        "OUT" => $"-{Quantity}",
        _ => Quantity.ToString()
    };
}
