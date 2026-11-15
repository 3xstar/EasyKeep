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
public partial class StocksView : UserControl, IRefreshableScreen
{
    private List<DeficitRow> _rows = new();
    private bool _isReady;
    private string _statusFilter = "all";

    public StocksView()
    {
        InitializeComponent();

        // XAML-строки переключаются словарём сами; кодовые сводки — по событию.
        // Подписка обновляется в Loaded: экран кэшируется и при повторном
        // показе снова должен реагировать на смену языка.
        Unloaded += (_, _) => LocalizationManager.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>Пересчитывает подписи, собранные в коде, после смены языка.</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Пункт «Все склады» — локальный объект-заглушка, не связанный с БД.
        if (WarehouseFilter.ItemsSource is List<Warehouse> warehouses &&
            warehouses.Count > 0 && warehouses[0].Id == 0)
        {
            int selectedIndex = WarehouseFilter.SelectedIndex;
            warehouses[0].Name = LocalizationManager.Get("Common_WarehouseAll");
            WarehouseFilter.ItemsSource = null;
            WarehouseFilter.ItemsSource = warehouses;
            WarehouseFilter.SelectedIndex = selectedIndex;
        }

        ApplyFilter();
    }

    private async void StocksView_Loaded(object sender, RoutedEventArgs e)
    {
        // Экран кэшируется: при повторном показе снова подписываемся на язык.
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
        LocalizationManager.LanguageChanged += OnLanguageChanged;

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

    /// <summary>Повторный вход в раздел: обновляем данные без пересоздания XAML.</summary>
    public async Task RefreshAsync()
    {
        if (!_isReady)
            return;

        await LoadRowsAsync();
    }

    /// <summary>Загружает склады и добавляет пункт «Все склады».</summary>
    private async Task LoadWarehousesAsync()
    {
        // Контекст EF создаётся и используется целиком в фоновом потоке:
        // UI-поток свободен и анимация перехода не прерывается.
        List<Warehouse> warehouses = await Task.Run(async () =>
        {
            using EasyKeepContext context = new();
            return await new WarehouseService(context).GetWarehousesAsync();
        });

        warehouses.Insert(0, new Warehouse { Id = 0, Name = LocalizationManager.Get("Common_WarehouseAll") });
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
            _rows = await Task.Run(async () =>
            {
                using EasyKeepContext context = new();
                return await new DeficitService(context).GetDeficitRowsAsync(warehouseId);
            });

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
            ? string.Format(LocalizationManager.Get("Stocks_SummaryAll"), visible.Count, criticalCount)
            : string.Format(LocalizationManager.Get("Stocks_SummaryFiltered"), visible.Count, _rows.Count);

        StateText.Text = _rows.Count == 0
            ? LocalizationManager.Get("Stocks_EmptyWarehouse")
            : LocalizationManager.Get("Stocks_EmptyFilter");
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
        SummaryText.Text = LocalizationManager.Get("Common_NoData");
        KpiAllText.Text = "—";
        KpiCriticalText.Text = "—";
        KpiWarningText.Text = "—";
        KpiNonLiquidText.Text = "—";
        StateText.Text = LocalizationManager.Get("Stocks_LoadError");
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

    /// <summary>
    /// Переключает «Светофор» между бейджами и компактными индикаторами.
    /// Колонка физически сужается/расширяется, поэтому освободившиеся
    /// пиксели сразу забирают соседние *-колонки «Товар» и «Склад».
    /// </summary>
    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        _isCompactTrafficLight = !_isCompactTrafficLight;

        // Шаблон меняется у колонки целиком: виртуализация строк остаётся корректной.
        TrafficLightColumn.CellTemplate = (DataTemplate)FindResource(
            _isCompactTrafficLight ? "CompactTrafficLightTemplate" : "FullTrafficLightTemplate");

        // Пиксельная ширина вместо Auto: DataGrid пересчитывает *-колонки
        // только при фактическом изменении Width, поэтому меняем и MinWidth.
        TrafficLightColumn.MinWidth = _isCompactTrafficLight ? 44 : 136;
        TrafficLightColumn.Width = new DataGridLength(
            _isCompactTrafficLight ? 44 : 136,
            DataGridLengthUnitType.Pixel);
        StocksGrid.UpdateLayout();

        // В полном режиме — три кружка, в компактном — один.
        MultiDotIcon.Visibility = _isCompactTrafficLight ? Visibility.Collapsed : Visibility.Visible;
        SingleDotIcon.Visibility = _isCompactTrafficLight ? Visibility.Visible : Visibility.Collapsed;

        CompactModeButton.ToolTip = "Переключить вид светофора";
        CompactModeButton.Foreground = _isCompactTrafficLight
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextSecondaryBrush");
    }
}
