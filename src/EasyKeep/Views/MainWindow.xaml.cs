using System.Windows;
using System.Windows.Controls;

namespace EasyKeep.Views;

/// <summary>
/// Главное окно EasyKeep: боковое меню и рабочая область экранов.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // По умолчанию открываем самый важный раздел — остатки.
        StocksNav.IsChecked = true;
    }

    /// <summary>Переключает экран по пункту меню.</summary>
    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string section })
            return;

        ShowSection(section);
    }

    private void ShowSection(string section)
    {
        (string title, string subtitle) = section switch
        {
            "movements" => ("Движения", "Журнал операций и проведение прихода, расхода и перемещений."),
            "catalog" => ("Каталог", "Товары, категории и полная история движений по каждой позиции."),
            "reports" => ("Отчёт", "Сводка запасов по складам и рейтинг самых активных товаров."),
            _ => ("Остатки", "Текущие остатки складов и «Светофор дефицита».")
        };

        PageTitleText.Text = title;
        PageSubtitleText.Text = subtitle;

        UserControl? view = section switch
        {
            "stocks" => new StocksView(),
            "movements" => new MovementsView(),
            "catalog" => new CatalogView(),
            _ => null
        };

        if (view is not null)
        {
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            ContentArea.Content = view;
            ContentArea.Visibility = Visibility.Visible;
            return;
        }

        // Остальные экраны подключаются отдельными шагами.
        ContentArea.Content = null;
        ContentArea.Visibility = Visibility.Collapsed;
        PlaceholderPanel.Visibility = Visibility.Visible;
        PlaceholderTitle.Text = title;
    }
}
