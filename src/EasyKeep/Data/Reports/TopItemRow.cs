namespace EasyKeep.Data.Reports;

/// <summary>Строка рейтинга самых активных товаров за период.</summary>
public record TopItemRow(
    string Sku,
    string Name,
    int OutQuantity,
    int TransferQuantity,
    int MovementsCount);
