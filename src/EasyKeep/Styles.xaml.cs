using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EasyKeep;

/// <summary>
/// Код-бихайнд словаря стилей: обработчики EventSetter обязаны жить
/// в классе самого словаря, поэтому логика сквозной прокрутки и
/// сортировки по кнопке-иконке находится здесь.
/// </summary>
public partial class StylesResources : ResourceDictionary
{
    public StylesResources()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Сортировка строго по клику на иконку: любой клик по тексту или
    /// пустой области шапки гасится, чтобы DataGrid не менял порядок
    /// строк сам. Клики внутри кнопки сортировки и кнопки светофора
    /// пропускаются к своим обработчикам.
    /// </summary>
    private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            FindHeaderButton(source) is not null)
        {
            return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Трёхступенчатый цикл по клику на иконку:
    /// по возрастанию → по убыванию → исходный порядок.
    /// </summary>
    private void SortButton_Click(object sender, RoutedEventArgs e)
    {
        // Иначе Click всплывёт до DataGridColumnHeader и запустит
        // встроенную сортировку вторым проходом.
        e.Handled = true;

        if (sender is not FrameworkElement { TemplatedParent: DataGridColumnHeader header })
            return;

        DataGridColumn? column = header.Column;
        if (column is null || !column.CanUserSort)
            return;

        DataGrid? grid = FindOwnerGrid(header);
        if (grid is null)
            return;

        // Незавершённая правка ячейки должна попасть в данные до сортировки.
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);

        ListSortDirection? nextDirection = column.SortDirection switch
        {
            ListSortDirection.Ascending => ListSortDirection.Descending,
            ListSortDirection.Descending => null,
            _ => ListSortDirection.Ascending
        };

        ClearOtherSortDirections(grid, column);

        if (nextDirection is null)
        {
            column.SortDirection = null;
            grid.Items.SortDescriptions.Clear();
            return;
        }

        string sortPath = column.SortMemberPath;
        if (string.IsNullOrEmpty(sortPath))
            return;

        column.SortDirection = nextDirection;
        try
        {
            using (grid.Items.DeferRefresh())
            {
                grid.Items.SortDescriptions.Clear();
                grid.Items.SortDescriptions.Add(new SortDescription(sortPath, nextDirection.Value));
            }
        }
        catch (InvalidOperationException)
        {
            // Источник данных не поддерживает сортировку — оставляем
            // исходный порядок и честно убираем стрелку.
            grid.Items.SortDescriptions.Clear();
            column.SortDirection = null;
        }
    }

    private static void ClearOtherSortDirections(DataGrid grid, DataGridColumn column)
    {
        foreach (DataGridColumn other in grid.Columns)
        {
            if (!ReferenceEquals(other, column))
                other.SortDirection = null;
        }
    }

    private static DataGrid? FindOwnerGrid(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null;)
        {
            if (current is DataGrid grid)
                return grid;

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>Ищет интерактивную кнопку шапки под курсором.</summary>
    private static Button? FindHeaderButton(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null;)
        {
            if (current is DataGridColumnHeader)
                return null;

            // Любая кнопка внутри шапки (сортировка, светофор) должна
            // получать клик — гасим только клики по тексту и пустоте.
            if (current is Button button)
                return button;

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
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
