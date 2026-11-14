using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        // XAML-строки переключаются словарём сами; кодовые — по событию.
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LocalizationManager.LanguageChanged -= OnLanguageChanged;
    }

    /// <summary>Пересчитывает кодовые строки каталога после смены языка.</summary>
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Пункт «Все категории» — локальная заглушка.
        if (CategoryFilter.ItemsSource is List<Category> categories &&
            categories.Count > 0 && categories[0].Id == 0)
        {
            int selectedIndex = CategoryFilter.SelectedIndex;
            categories[0].Name = LocalizationManager.Get("Common_CategoryAll");
            CategoryFilter.ItemsSource = null;
            CategoryFilter.ItemsSource = categories;
            CategoryFilter.SelectedIndex = selectedIndex;
        }

        ApplyFilter();

        // «Рентген»: перезагружаем историю выбранного товара или сбрасываем подписи.
        if (ItemsGrid.SelectedItem is Item)
        {
            ItemsGrid_SelectionChanged(
                ItemsGrid,
                new SelectionChangedEventArgs(Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
        }
        else
        {
            ResetXRay();
        }
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
            filterCategories.Insert(0, new Category { Id = 0, Name = LocalizationManager.Get("Common_CategoryAll") });

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
        SummaryText.Text = string.Format(LocalizationManager.Get("Cat_ItemsSummary"), visible.Count);

        StateText.Text = _items.Count == 0
            ? LocalizationManager.Get("Cat_EmptyCatalog")
            : LocalizationManager.Get("Cat_EmptyFilter");
        StateText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowLoadError()
    {
        ItemsGrid.ItemsSource = null;
        SummaryText.Text = LocalizationManager.Get("Common_NoData");
        StateText.Text = LocalizationManager.Get("Cat_LoadError");
        StateText.Visibility = Visibility.Visible;
    }

    // ==================== Товары ====================

    private async void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (_categories.Count == 0)
        {
            ShowWarning(LocalizationManager.Get("Cat_NeedCategory"));
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
            ShowWarning(LocalizationManager.Get("Cat_CreateError"));
        }
    }

    private async void EditItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Item item)
        {
            ShowWarning(LocalizationManager.Get("Cat_SelectItem"));
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
            ShowWarning(LocalizationManager.Get("Cat_UpdateError"));
        }
    }

    private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not Item item)
        {
            ShowWarning(LocalizationManager.Get("Cat_SelectItem"));
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            string.Format(LocalizationManager.Get("Cat_DeleteItemConfirm"), item.Name, item.Sku) + "\n" +
            LocalizationManager.Get("Common_DeleteIrreversible"),
            LocalizationManager.Get("Common_ConfirmDeleteTitle"),
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
            ShowWarning(LocalizationManager.Get("Cat_DeleteError"));
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
        XRayMeta.Text = $"{item.Sku} · {item.Category?.Name ?? LocalizationManager.Get("Cat_NoCategory")} · {item.Unit}";
        HistoryList.ItemsSource = null;
        HistoryStateText.Text = LocalizationManager.Get("Cat_XRayLoading");
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
            HistoryStateText.Text = LocalizationManager.Get("Cat_XRayEmptyHistory");
            HistoryStateText.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            HistoryList.ItemsSource = null;
            HistoryStateText.Text = LocalizationManager.Get("Cat_XRayHistoryError");
            HistoryStateText.Visibility = Visibility.Visible;
        }
    }

    private void ResetXRay()
    {
        XRayItemName.Text = LocalizationManager.Get("Cat_XRayNoItem");
        XRayMeta.Text = LocalizationManager.Get("Cat_XRayNoItemHint");
        HistoryList.ItemsSource = null;
        HistoryStateText.Text = LocalizationManager.Get("Cat_SelectItem");
        HistoryStateText.Visibility = Visibility.Visible;
    }

    /// <summary>Панель истории мягко выезжает справа за 200 мс (кривая iOS).</summary>
    private void AnimateXRayPanel()
    {
        QuinticEase easing = new() { EasingMode = EasingMode.EaseOut };
        TimeSpan duration = TimeSpan.FromMilliseconds(200);

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
        MessageBox.Show(message, LocalizationManager.Get("Cat_WarnTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
