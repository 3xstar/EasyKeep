namespace EasyKeep.Services;

/// <summary>Нехватка товара на складе-отправителе.</summary>
public record StockShortage(
    string Sku,
    string ItemName,
    int Available,
    int Requested)
{
    /// <summary>Подсказка пользователю с доступным максимумом.</summary>
    public string Message =>
        $"«{ItemName}»: доступно {Available}, запрошено {Requested}. Можно провести не больше {Available}.";
}
