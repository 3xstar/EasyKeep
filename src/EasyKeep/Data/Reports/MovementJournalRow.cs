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
    int TotalQuantity);
