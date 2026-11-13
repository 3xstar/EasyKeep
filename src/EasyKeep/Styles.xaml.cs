using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EasyKeep;

/// <summary>
/// Код-бихайнд словаря стилей: обработчики EventSetter обязаны жить
/// в классе самого словаря, поэтому логика сквозной прокрутки здесь.
/// </summary>
public partial class StylesResources : ResourceDictionary
{
    public StylesResources()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Сквозная прокрутка страницы: если внутренний ScrollViewer таблицы
    /// не может прокрутить дальше в направлении колеса (или прокрутки
    /// внутри таблицы нет), событие перенаправляется родительскому
    /// ScrollViewer страницы.
    /// </summary>
    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        ScrollViewer? inner = FindInnerScrollViewer(grid);
        bool scrollingUp = e.Delta > 0;
        bool tableCanScroll = inner is not null &&
                              (scrollingUp
                                  ? inner.VerticalOffset > 0
                                  : inner.VerticalOffset < inner.ScrollableHeight);

        if (tableCanScroll)
            return;

        e.Handled = true;
        MouseWheelEventArgs redirect = new(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = grid
        };
        grid.RaiseEvent(redirect);
    }

    private static ScrollViewer? FindInnerScrollViewer(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is ScrollViewer viewer)
                return viewer;

            ScrollViewer? nested = FindInnerScrollViewer(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }
}
