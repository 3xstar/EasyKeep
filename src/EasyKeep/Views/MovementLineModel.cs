using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasyKeep.Views;

/// <summary>
/// Строка документа движения в интерфейсе.
/// Количество можно менять прямо в таблице.
/// </summary>
public class MovementLineModel : INotifyPropertyChanged
{
    private int _quantity = 1;

    public int ItemId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (value == _quantity)
                return;

            _quantity = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
