namespace EasyKeep.Data;

/// <summary>
/// Ошибка нехватки остатка. Хранит доступное и запрошенное количество,
/// чтобы интерфейс мог предложить подставить максимум.
/// </summary>
public class InsufficientStockException : Exception
{
    public string ItemName { get; }
    public int Available { get; }
    public int Requested { get; }

    public InsufficientStockException(string itemName, int available, int requested)
        : base($"Недостаточно товара «{itemName}»: доступно {available}, запрошено {requested}.")
    {
        ItemName = itemName;
        Available = available;
        Requested = requested;
    }
}
