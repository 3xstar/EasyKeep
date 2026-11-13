using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EasyKeep.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Data.Reports;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Экран «Каталог»: CRUD товаров и категорий, поиск и панель «Рентген товара».
/// </summary>
public partial class CatalogView : UserControl
{
    private List<Item> _items = new();
    private List<Category> _categories = new();
    private bool _isReady;

    public CatalogView()
    {
        InitializeComponent();
    }

    private async void CatalogView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isReady)
            return;

        await ReloadAsync();
    }

    /// <summary>Загружает товары и категории, затем применяет фильтры.</summary>
    private async Task ReloadAsync()
    {
        try
        {
            using EasyKeepContext context = new();
            CatalogService service = new(context);

            _categories = await service.GetCategoriesAsync();
            _items = await service.GetItemsAsync();

            List<Category> filterCategories = new(_categories);
            filterCategories.Insert(0, new Category { Id = 0, Name = "Все категории" });

            CategoryFilter.ItemsSource = filterCategories;
            CategoryFilter.SelectedIndex = 0;

            _isReady = true;
            ApplyFilter();
        }
        catch (Exception)
        {
            ShowLoadError();
        }
    }

    private void ApplyFilter()
    {
        int? categoryId = (CategoryFilter.SelectedItem as Category)?.Id;
        if (categoryId == 0)
            categoryId = null;

        string search = SearchBox.Text.Trim();

        List<Item> visible = _items
            .Where(item => (categoryId is null || item.CategoryId == categoryId)
                           && (search.Length == 0
                               || item.Sku.Contains(search, StringComparison.OrdinalIgnoreCase)
                               || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        ItemsGrid.ItemsSource = visible;
        SummaryText.Text = $"Товаров: {visible.Count}";

        StateText.Text = _items.Count == 0
            ? "Каталог пуст. Добавьте первый товар."
            : "По вашему запросу ничего не найдено. Измените поиск или категорию.";
        StateText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowLoadError()
    {
        ItemsGrid.ItemsSource = null;
        SummaryText.Text = "Нет данных";
        StateText.Text = "Не удалось загрузить каталог. Проверьте подключение к базе данных.";
        StateText.Visibility = Visibility.Visible;
    }

    // ==================== Товары ====================

    private async void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (_categories.Count == 0)
        {
            ShowWarning("Сначала создайте хотя бы одну категорию через кнопку «Категории».");
            return;
        }

        ItemEditorDialog dialog = new(_categories);
        SetOwner(dialog);

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            using EasyKeepContext context = new();
            await new CatalogService(context).CreateItemAsync(
                dialog.Sku,
                dialog.ItemName,
                dialog.CategoryId,
                dialog.Unit,
                dialog.Price,
                dialog.MinStock);

            await ReloadAsync();
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception)
        {
            ShowWarning("Не удалось добавить товар. Проверьте подключение к базе данных.");
        }
    }

    private async void EditItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Item item)
        {
            ShowWarning("Выберите товар в таблице.");
            return;
        }

        ItemEditorDialog dialog = new(_categories, item);
        SetOwner(dialog);

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            using EasyKeepContext context = new();
            await new CatalogService(context).UpdateItemAsync(
                item.Id,
                dialog.Sku,
                dialog.ItemName,
                dialog.CategoryId,
                dialog.Unit,
                dialog.Price,
                dialog.MinStock);

            await ReloadAsync();
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception)
        {
            ShowWarning("Не удалось изменить товар. Проверьте подключение к базе данных.");
        }
    }

    private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Item item)
        {
            ShowWarning("Выберите товар в таблице.");
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            $"Удалить товар «{item.Name}» ({item.Sku})?\nЭто действие необратимо.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            using EasyKeepContext context = new();
            await new CatalogService(context).DeleteItemAsync(item.Id);
            await ReloadAsync();
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception)
        {
            ShowWarning("Не удалось удалить товар. Проверьте подключение к базе данных.");
        }
    }

    private async void CategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        CategoryManagerDialog dialog = new();
        SetOwner(dialog);
        dialog.ShowDialog();

        // После закрытия диалога категории могли измениться.
        await ReloadAsync();
    }

    // ==================== Рентген товара ====================

    private async void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Item item)
        {
            ResetXRay();
            return;
        }

        XRayItemName.Text = item.Name;
        XRayMeta.Text = $"{item.Sku} · {item.Category?.Name ?? "Без категории"} · {item.Unit}";
        HistoryList.ItemsSource = null;
        HistoryStateText.Text = "Загрузка истории...";
        HistoryStateText.Visibility = Visibility.Visible;
        AnimateXRayPanel();

        try
        {
            List<ItemMovementRow> history;
            using (EasyKeepContext context = new())
            {
                history = new CatalogService(context).GetItemHistory(item.Id);
            }

            // Пользователь мог выбрать другой товар, пока шла загрузка.
            if (ItemsGrid.SelectedItem is not Item current || current.Id != item.Id)
                return;

            HistoryList.ItemsSource = history;
            HistoryStateText.Text = "У этого товара пока нет движений.";
            HistoryStateText.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            HistoryList.ItemsSource = null;
            HistoryStateText.Text = "Не удалось загрузить историю. Проверьте подключение к базе данных.";
            HistoryStateText.Visibility = Visibility.Visible;
        }
    }

    private void ResetXRay()
    {
        XRayItemName.Text = "Товар не выбран";
        XRayMeta.Text = "Кликните по строке таблицы, чтобы увидеть историю.";
        HistoryList.ItemsSource = null;
        HistoryStateText.Text = "Выберите товар в таблице.";
        HistoryStateText.Visibility = Visibility.Visible;
    }

    /// <summary>Панель истории мягко выезжает справа за 140 мс.</summary>
    private void AnimateXRayPanel()
    {
        CubicEase easing = new() { EasingMode = EasingMode.EaseOut };
        TimeSpan duration = TimeSpan.FromMilliseconds(140);

        XRayPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0.7, 1, duration)
        {
            EasingFunction = easing
        });

        XRayShift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(16, 0, duration)
        {
            EasingFunction = easing
        });
    }

    // ==================== Вспомогательные методы ====================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isReady)
            ApplyFilter();
    }

    private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isReady)
            ApplyFilter();
    }

    private void SetOwner(Window dialog)
    {
        Window? owner = Window.GetWindow(this);
        if (owner is not null)
            dialog.Owner = owner;
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "Каталог", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
