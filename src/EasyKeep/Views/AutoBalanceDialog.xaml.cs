using System.Windows;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>Строка предпросмотра черновика авто-балансировки.</summary>
public record AutoBalancePreviewRow(
    string WarehouseName,
    string Sku,
    string ItemName,
    int Quantity);

/// <summary>
/// Диалог проверки и подтверждения черновиков авто-балансировки.
/// </summary>
public partial class AutoBalanceDialog : Window
{
    public AutoBalanceDialog(IReadOnlyList<AutoBalancePreviewRow> rows)
    {
        InitializeComponent();

        LocalizationManager.ApplyCulture(this);
        Title = LocalizationManager.Get("AbDialog_Title");

        DraftsGrid.ItemsSource = rows;

        int warehousesCount = rows.Select(row => row.WarehouseName).Distinct().Count();
        int totalQuantity = rows.Sum(row => row.Quantity);
        SummaryText.Text = string.Format(
            LocalizationManager.Get("AbDialog_Summary"),
            warehousesCount,
            rows.Count,
            totalQuantity);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
