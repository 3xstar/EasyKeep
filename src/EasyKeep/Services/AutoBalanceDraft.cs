using EasyKeep.Data.Entities;

namespace EasyKeep.Services;

/// <summary>
/// Черновик перемещения для одной торговой точки.
/// Документ ещё не проведён — пользователь проверяет его и подтверждает.
/// </summary>
public record AutoBalanceDraft(
    int ToWarehouseId,
    string ToWarehouseName,
    Movement Movement)
{
    public int LinesCount => Movement.Items.Count;

    public int TotalQuantity => Movement.Items.Sum(line => line.Quantity);
}
