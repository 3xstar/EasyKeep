using System.Windows;
using System.Windows.Controls;
using EasyKeep.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Экран «Остатки»: таблица остатков и «Светофор дефицита»
/// с фильтром по складу и поиском по SKU/названию.
/// </summary>
public partial class StocksView : UserControl
{
    private List<DeficitRow> _rows = new();
    private bool _isReady;

    public StocksView()
    {
        InitializeComponent();
    }

    private async void StocksView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isReady)
            return;

        try
        {
            await LoadWarehousesAsync();
            _isReady = true;
            await LoadRowsAsync();
        }
        catch (Exception)
        {
            ShowLoadError();
        }
    }

    /// <summary>Загружает склады и добавляет пункт «Все склады».</summary>
    private async Task LoadWarehousesAsync()
    {
        List<Warehouse> warehouses;

        using (EasyKeepContext context = new())
        {
            warehouses = await new WarehouseService(context).GetWarehousesAsync();
        }

        warehouses.Insert(0, new Warehouse { Id = 0, Name = "Все склады" });
        WarehouseFilter.ItemsSource = warehouses;
        WarehouseFilter.SelectedIndex = 0;
    }

    private async Task LoadRowsAsync()
    {
        int? warehouseId = (WarehouseFilter.SelectedItem as Warehouse)?.Id;
        if (warehouseId == 0)
            warehouseId = null;

        try
        {
            using EasyKeepContext context = new();
            _rows = await new DeficitService(context).GetDeficitRowsAsync(warehouseId);
            ApplyFilter();
        }
        catch (Exception)
        {
            _rows = new List<DeficitRow>();
            ShowLoadError();
        }
    }

    /// <summary>Поиск выполняется по загруженным строкам, без запросов к БД.</summary>
    private void ApplyFilter()
    {
        string search = SearchBox.Text.Trim();

        List<DeficitRow> visible = _rows
            .Where(row => search.Length == 0
                          || row.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)
                          || row.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        StocksGrid.ItemsSource = visible;

        int criticalCount = visible.Count(row => row.Status == DeficitStatus.Critical);
        SummaryText.Text = $"Позиций: {visible.Count} · критично: {criticalCount}";

        StateText.Text = _rows.Count == 0
            ? "На выбранном складе пока нет остатков."
            : "По вашему запросу ничего не найдено. Измените поиск или фильтр.";
        StateText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowLoadError()
    {
        StocksGrid.ItemsSource = null;
        SummaryText.Text = "Нет данных";
        StateText.Text = "Не удалось загрузить остатки. Проверьте подключение к базе данных.";
        StateText.Visibility = Visibility.Visible;
    }

    private async void WarehouseFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady || !IsLoaded)
            return;

        await LoadRowsAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadRowsAsync();
    }
}
