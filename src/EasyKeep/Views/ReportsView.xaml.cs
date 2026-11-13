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

            StockStateText.Text = "Остатков пока нет.";
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
            ResetLeader("укажите корректный период");
            TopStateText.Text = "Дата начала должна быть раньше даты окончания.";
            TopStateText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            List<TopItemRow> rows = new ReportService().GetTopItems(from, to);

            TopItemsGrid.ItemsSource = rows;
            TopSummaryText.Text = $"Показано: {rows.Count}";
            TopStateText.Text = "За выбранный период движений не найдено.";
            TopStateText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (rows.Count > 0)
            {
                TopItemRow leader = rows[0];
                LeaderNameText.Text = leader.Name;
                LeaderDetailText.Text = $"{leader.Sku} · {leader.OutQuantity:N0} ед. расхода";
            }
            else
            {
                ResetLeader("за выбранный период движений нет");
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
        StockStateText.Text = "Не удалось загрузить отчёт. Проверьте подключение к базе данных.";
        StockStateText.Visibility = Visibility.Visible;
    }

    private void ShowTopItemsError()
    {
        TopItemsGrid.ItemsSource = null;
        TopSummaryText.Text = "Нет данных";
        ResetLeader("нет данных");
        TopStateText.Text = "Не удалось построить рейтинг. Проверьте подключение к базе данных.";
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
