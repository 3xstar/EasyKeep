namespace EasyKeep.Data.Reports;

/// <summary>Одно событие в истории выбранного товара («Рентген товара»).</summary>
public record ItemMovementRow(
    int MovementId,
    DateTime CreatedAt,
    string Type,
    string? FromWarehouse,
    string? ToWarehouse,
    int Quantity,
    string? Comment);
