using System.Windows;
using System.Windows.Controls;
using EasyKeep.Data.Reports;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Экран «Отчёт»: сводка остатков по складам и рейтинг активных товаров.
/// </summary>
public partial class ReportsView : UserControl
{
    private bool _isReady;

    public ReportsView()
    {
        InitializeComponent();

        // XAML-строки переключаются словарём сами; кодовые — по событию.
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LocalizationManager.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>Пересобирает оба отчёта, чтобы кодовые подписи сменили язык.</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!_isReady || !IsLoaded)
            return;

        LoadStockReport();
        LoadTopItems();
    }

    private void ReportsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isReady)
            return;

        _isReady = true;
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
        ToDatePicker.SelectedDate = DateTime.Today;

        LoadStockReport();
        LoadTopItems();
    }

    private void LoadStockReport()
    {
        try
        {
            List<WarehouseStockRow> rows = new ReportService().GetStockReport();

            StockGrid.ItemsSource = rows;
            WarehouseCountText.Text = rows.Count.ToString("N0");
            TotalQuantityText.Text = rows.Sum(row => row.TotalQuantity).ToString("N0");
            TotalValueText.Text = rows.Sum(row => row.TotalValue).ToString("N0") + " ₽";

            StockStateText.Text = LocalizationManager.Get("Rep_StockEmpty");
            StockStateText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            ShowStockError();
        }
    }

    private void LoadTopItems()
    {
        DateTime from = FromDatePicker.SelectedDate?.Date ?? DateTime.Today.AddDays(-30);
        DateTime to = (ToDatePicker.SelectedDate?.Date ?? DateTime.Today).AddDays(1);

        if (to <= from)
        {
            TopItemsGrid.ItemsSource = null;
            TopSummaryText.Text = "—";
            ResetLeader(LocalizationManager.Get("Rep_LeaderInvalidPeriod"));
            TopStateText.Text = LocalizationManager.Get("Common_InvalidPeriod");
            TopStateText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            List<TopItemRow> rows = new ReportService().GetTopItems(from, to);

            TopItemsGrid.ItemsSource = rows;
            TopSummaryText.Text = string.Format(LocalizationManager.Get("Rep_TopSummary"), rows.Count);
            TopStateText.Text = LocalizationManager.Get("Rep_TopEmpty");
            TopStateText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (rows.Count > 0)
            {
                TopItemRow leader = rows[0];
                LeaderNameText.Text = leader.Name;
                LeaderDetailText.Text = string.Format(
                    LocalizationManager.Get("Rep_LeaderDetail"),
                    leader.Sku,
                    leader.OutQuantity.ToString("N0"));
            }
            else
            {
                ResetLeader(LocalizationManager.Get("Rep_LeaderNoMovements"));
            }
        }
        catch (Exception)
        {
            ShowTopItemsError();
        }
    }

    private void ShowStockError()
    {
        StockGrid.ItemsSource = null;
        WarehouseCountText.Text = "—";
        TotalQuantityText.Text = "—";
        TotalValueText.Text = "—";
        StockStateText.Text = LocalizationManager.Get("Rep_StockError");
        StockStateText.Visibility = Visibility.Visible;
    }

    private void ShowTopItemsError()
    {
        TopItemsGrid.ItemsSource = null;
        TopSummaryText.Text = LocalizationManager.Get("Common_NoData");
        ResetLeader(LocalizationManager.Get("Rep_LeaderNoData"));
        TopStateText.Text = LocalizationManager.Get("Rep_TopError");
        TopStateText.Visibility = Visibility.Visible;
    }

    /// <summary>Сбрасывает карточку лидера оборота в состояние «нет данных».</summary>
    private void ResetLeader(string hint)
    {
        LeaderNameText.Text = "—";
        LeaderDetailText.Text = hint;
    }

    private void RefreshStockButton_Click(object sender, RoutedEventArgs e)
    {
        LoadStockReport();
    }

    private void BuildTopButton_Click(object sender, RoutedEventArgs e)
    {
        LoadTopItems();
    }
}
