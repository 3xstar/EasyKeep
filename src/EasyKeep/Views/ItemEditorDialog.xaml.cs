using System.Globalization;
using System.Windows;
using EasyKeep.Data.Entities;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Диалог создания и редактирования товара.
/// Сохраняет введённые значения в свойства, запись выполняет вызывающий экран.
/// </summary>
public partial class ItemEditorDialog : Window
{
    public ItemEditorDialog(IReadOnlyList<Category> categories, Item? item = null)
    {
        InitializeComponent();

        LocalizationManager.ApplyCulture(this);
        Title = LocalizationManager.Get("ItemDialog_Title");

        CategoryCombo.ItemsSource = categories;

        if (item is null)
        {
            UnitBox.Text = LocalizationManager.Get("ItemDialog_UnitDefault");
            PriceBox.Text = "0";
            MinStockBox.Text = "0";
            CategoryCombo.SelectedIndex = categories.Count > 0 ? 0 : -1;
            return;
        }

        HeaderText.Text = LocalizationManager.Get("ItemDialog_Edit");
        SkuBox.Text = item.Sku;
        NameBox.Text = item.Name;
        CategoryCombo.SelectedValue = item.CategoryId;
        UnitBox.Text = item.Unit;
        PriceBox.Text = item.Price.ToString("0.##", CultureInfo.CurrentCulture);
        MinStockBox.Text = item.MinStock.ToString(CultureInfo.CurrentCulture);
    }

    public string Sku => SkuBox.Text.Trim();

    public string ItemName => NameBox.Text.Trim();

    public int CategoryId => CategoryCombo.SelectedValue is int id ? id : 0;

    public string Unit => UnitBox.Text.Trim();

    public decimal Price { get; private set; }

    public int MinStock { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput())
            return;

        DialogResult = true;
    }

    private bool ValidateInput()
    {
        if (Sku.Length == 0 || ItemName.Length == 0 || Unit.Length == 0)
        {
            ShowWarning(LocalizationManager.Get("ItemDialog_WarnRequired"));
            return false;
        }

        if (CategoryId == 0)
        {
            ShowWarning(LocalizationManager.Get("ItemDialog_WarnNoCategory"));
            return false;
        }

        if (!decimal.TryParse(PriceBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal price) ||
            price < 0)
        {
            ShowWarning(LocalizationManager.Get("ItemDialog_WarnPrice"));
            return false;
        }

        if (!int.TryParse(MinStockBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int minStock) ||
            minStock < 0)
        {
            ShowWarning(LocalizationManager.Get("ItemDialog_WarnMinStock"));
            return false;
        }

        Price = price;
        MinStock = minStock;
        return true;
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, LocalizationManager.Get("ItemDialog_WarnTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
