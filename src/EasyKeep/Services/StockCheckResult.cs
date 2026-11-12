namespace EasyKeep.Services;

/// <summary>Результат проверки остатков перед проведением документа.</summary>
public record StockCheckResult(
    bool IsEnough,
    IReadOnlyList<StockShortage> Shortages)
{
    public string Message => IsEnough
        ? "Остатка достаточно."
        : Shortages[0].Message;
}
