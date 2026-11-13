using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private string _statusFilter = "all";

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

    /// <summary>Поиск и быстрый фильтр по виджету выполняются по загруженным строкам, без запросов к БД.</summary>
    private void ApplyFilter()
    {
        string search = SearchBox.Text.Trim();

        List<DeficitRow> visible = _rows
            .Where(row => MatchesStatus(row) && MatchesSearch(row, search))
            .ToList();

        StocksGrid.ItemsSource = visible;

        UpdateKpiCards();

        int criticalCount = visible.Count(row => row.Status == DeficitStatus.Critical);
        SummaryText.Text = _statusFilter == "all"
            ? $"Позиций: {visible.Count} · критично: {criticalCount}"
            : $"Показано: {visible.Count} из {_rows.Count}";

        StateText.Text = _rows.Count == 0
            ? "На выбранном складе пока нет остатков."
            : "По вашему запросу ничего не найдено. Измените поиск, склад или виджет.";
        StateText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool MatchesSearch(DeficitRow row, string search)
    {
        return search.Length == 0
               || row.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)
               || row.ItemName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesStatus(DeficitRow row)
    {
        return _statusFilter switch
        {
            "critical" => row.Status == DeficitStatus.Critical,
            "warning" => row.Status == DeficitStatus.Warning,
            "nonliquid" => row.Status == DeficitStatus.NonLiquid,
            _ => true
        };
    }

    /// <summary>Счётчики KPI берутся из уже загруженных строк — без новых SQL-запросов.</summary>
    private void UpdateKpiCards()
    {
        KpiAllText.Text = _rows.Count.ToString("N0");
        KpiCriticalText.Text = _rows.Count(row => row.Status == DeficitStatus.Critical).ToString("N0");
        KpiWarningText.Text = _rows.Count(row => row.Status == DeficitStatus.Warning).ToString("N0");
        KpiNonLiquidText.Text = _rows.Count(row => row.Status == DeficitStatus.NonLiquid).ToString("N0");
    }

    private void KpiFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string filter })
            return;

        _statusFilter = filter;

        // На старте элементы панели фильтров ещё не созданы — данные придут из LoadRowsAsync.
        if (_isReady && SearchBox is not null)
            ApplyFilter();
    }

    private void ShowLoadError()
    {
        StocksGrid.ItemsSource = null;
        SummaryText.Text = "Нет данных";
        KpiAllText.Text = "—";
        KpiCriticalText.Text = "—";
        KpiWarningText.Text = "—";
        KpiNonLiquidText.Text = "—";
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
        if (_isReady)
            ApplyFilter();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadRowsAsync();
    }

    private bool _isCompactTrafficLight;

    /// <summary>Переключает «Светофор» между бейджами и компактными индикаторами.</summary>
    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        _isCompactTrafficLight = !_isCompactTrafficLight;

        // Шаблон меняется у колонки целиком: виртуализация строк остаётся корректной.
        TrafficLightColumn.CellTemplate = (DataTemplate)FindResource(
            _isCompactTrafficLight ? "CompactTrafficLightTemplate" : "FullTrafficLightTemplate");

        CompactModeButton.ToolTip = _isCompactTrafficLight
            ? "Обычный вид светофора (бейджи со статусами)"
            : "Компактный вид светофора (только индикаторы)";
        CompactModeButton.Foreground = _isCompactTrafficLight
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextSecondaryBrush");
    }
}
