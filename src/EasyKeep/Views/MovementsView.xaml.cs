using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EasyKeep.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Data.Reports;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Экран «Движения»: журнал операций, проведение IN/OUT/TRANSFER,
/// «Командная строка склада» и авто-балансировка.
/// </summary>
public partial class MovementsView : UserControl
{
    private readonly ObservableCollection<MovementLineModel> _lines = new();
    private List<Warehouse> _warehouses = new();
    private bool _isReady;

    public MovementsView()
    {
        InitializeComponent();

        LinesGrid.ItemsSource = _lines;
        _lines.CollectionChanged += Lines_CollectionChanged;

        // По умолчанию открываем расход — самую частую операцию.
        TypeCombo.SelectedIndex = 1;
        TypeFilter.SelectedIndex = 0;
        UpdateWarehouseVisibility();
        UpdateDocumentSummary();
    }

    private async void MovementsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isReady)
            return;

        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-30);
        ToDatePicker.SelectedDate = DateTime.Today;

        try
        {
            await LoadWarehousesAsync();
            _isReady = true;
            LoadJournal();
            CommandLineBox.Focus();
        }
        catch (Exception)
        {
            ShowConnectionError();
        }
    }

    /// <summary>Загружает склады для формы, фильтра журнала и подписей.</summary>
    private async Task LoadWarehousesAsync()
    {
        using (EasyKeepContext context = new())
        {
            _warehouses = await new WarehouseService(context).GetWarehousesAsync();
        }

        SourceWarehouse.ItemsSource = _warehouses;
        TargetWarehouse.ItemsSource = _warehouses;

        List<Warehouse> filterWarehouses = new(_warehouses);
        filterWarehouses.Insert(0, new Warehouse { Id = 0, Name = "Все склады" });

        JournalWarehouse.ItemsSource = filterWarehouses;

        if (_warehouses.Count > 0)
        {
            SourceWarehouse.SelectedIndex = 0;
            TargetWarehouse.SelectedIndex = _warehouses.Count > 1 ? 1 : 0;
        }

        JournalWarehouse.SelectedIndex = 0;
        EnsureDifferentWarehouses();
    }

    // ==================== Форма документа ====================

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SourceWarehousePanel is null)
            return;

        UpdateWarehouseVisibility();
        EnsureDifferentWarehouses();
    }

    private void UpdateWarehouseVisibility()
    {
        string type = GetSelectedType();
        bool needsSource = type is MovementTypes.Out or MovementTypes.Transfer;
        bool needsTarget = type is MovementTypes.In or MovementTypes.Transfer;

        SourceWarehousePanel.Visibility = needsSource ? Visibility.Visible : Visibility.Collapsed;
        TargetWarehousePanel.Visibility = needsTarget ? Visibility.Visible : Visibility.Collapsed;

        if (needsSource && needsTarget)
        {
            Grid.SetColumn(TargetWarehousePanel, 1);
            Grid.SetColumnSpan(SourceWarehousePanel, 1);
            Grid.SetColumnSpan(TargetWarehousePanel, 1);
            SourceWarehousePanel.Margin = new Thickness(0, 0, 6, 0);
            TargetWarehousePanel.Margin = new Thickness(6, 0, 0, 0);
        }
        else if (needsTarget)
        {
            Grid.SetColumn(TargetWarehousePanel, 0);
            Grid.SetColumnSpan(TargetWarehousePanel, 2);
            TargetWarehousePanel.Margin = new Thickness(0);
        }
        else
        {
            Grid.SetColumnSpan(SourceWarehousePanel, 2);
            SourceWarehousePanel.Margin = new Thickness(0);
        }
    }

    /// <summary>Для перемещения склады отправления и получения должны различаться.</summary>
    private void EnsureDifferentWarehouses()
    {
        if (GetSelectedType() != MovementTypes.Transfer || _warehouses.Count < 2)
            return;

        if (SourceWarehouse.SelectedItem is not Warehouse source ||
            TargetWarehouse.SelectedItem is not Warehouse target ||
            source.Id != target.Id)
            return;

        TargetWarehouse.SelectedItem = _warehouses.First(warehouse => warehouse.Id != source.Id);
    }

    /// <summary>
    /// «Командная строка склада»: Enter находит товар по SKU/названию
    /// и добавляет его в документ. Повторный ввод увеличивает количество.
    /// </summary>
    private async void CommandLineBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        await AddFromCommandLineAsync();
    }

    private async Task AddFromCommandLineAsync()
    {
        string query = CommandLineBox.Text.Trim();
        if (query.Length == 0)
            return;

        try
        {
            Item? item;
            using (EasyKeepContext context = new())
            {
                item = await new MovementService(context).FindItemAsync(query);
            }

            if (item is null)
            {
                ShowCommandFeedback($"Товар «{query}» не найден.", isError: true);
                return;
            }

            MovementLineModel? line = _lines.FirstOrDefault(existing => existing.ItemId == item.Id);
            if (line is not null)
            {
                line.Quantity += 1;
            }
            else
            {
                line = new MovementLineModel
                {
                    ItemId = item.Id,
                    Sku = item.Sku,
                    Name = item.Name,
                    Unit = item.Unit,
                    Quantity = 1
                };
                line.PropertyChanged += Line_PropertyChanged;
                _lines.Add(line);
            }

            LinesGrid.ScrollIntoView(line);
            ShowCommandFeedback($"Добавлено: {item.Name} (+1).", isError: false);
            CommandLineBox.Clear();
            CommandLineBox.Focus();
        }
        catch (Exception)
        {
            ShowCommandFeedback("Не удалось найти товар. Проверьте подключение к базе данных.", isError: true);
        }
    }

    private void ShowCommandFeedback(string message, bool isError)
    {
        CommandFeedbackText.Text = message;
        CommandFeedbackText.Foreground = (Brush)FindResource(isError ? "DangerBrush" : "SuccessBrush");
    }

    private void RemoveLine_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MovementLineModel line })
            RemoveLine(line);
    }

    private void RemoveLine(MovementLineModel line)
    {
        line.PropertyChanged -= Line_PropertyChanged;
        _lines.Remove(line);
    }

    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDocumentSummary();
    }

    private void Line_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MovementLineModel.Quantity))
            UpdateDocumentSummary();
    }

    private void UpdateDocumentSummary()
    {
        int totalQuantity = _lines.Sum(line => line.Quantity);
        DocumentSummaryText.Text = $"Позиций: {_lines.Count} · единиц: {totalQuantity}";
        LinesEmptyText.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        ClearDocument();
    }

    private void ClearDocument()
    {
        foreach (MovementLineModel line in _lines)
            line.PropertyChanged -= Line_PropertyChanged;

        _lines.Clear();
        CommentBox.Clear();
        CommandFeedbackText.Text = string.Empty;
        UpdateDocumentSummary();
        CommandLineBox.Focus();
    }

    private async void PostButton_Click(object sender, RoutedEventArgs e)
    {
        string type = GetSelectedType();

        if (!TryBuildMovement(type, out Movement? movement) || movement is null)
            return;

        string confirmation = BuildConfirmationText(type);
        MessageBoxResult answer = MessageBox.Show(
            confirmation,
            "Подтверждение операции",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            using EasyKeepContext context = new();
            int movementId = await new MovementService(context).PostMovementAsync(movement);

            MessageBox.Show(
                $"Документ №{movementId} успешно проведён. Остатки обновлены.",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            ClearDocument();
            LoadJournal();
        }
        catch (InsufficientStockException shortage)
        {
            HandleShortage(shortage);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось провести документ. {ex.Message}",
                "Ошибка проведения",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private bool TryBuildMovement(string type, out Movement? movement)
    {
        movement = null;

        if (_lines.Count == 0)
        {
            ShowWarning("Добавьте хотя бы одну позицию в документ.");
            return false;
        }

        if (_lines.Any(line => line.Quantity <= 0))
        {
            ShowWarning("Количество в каждой строке должно быть больше нуля.");
            return false;
        }

        Warehouse? source = SourceWarehouse.SelectedItem as Warehouse;
        Warehouse? target = TargetWarehouse.SelectedItem as Warehouse;
        bool needsSource = type is MovementTypes.Out or MovementTypes.Transfer;
        bool needsTarget = type is MovementTypes.In or MovementTypes.Transfer;

        if (needsSource && source is null)
        {
            ShowWarning("Выберите склад-источник.");
            return false;
        }

        if (needsTarget && target is null)
        {
            ShowWarning("Выберите склад-получатель.");
            return false;
        }

        if (type == MovementTypes.Transfer && source!.Id == target!.Id)
        {
            ShowWarning("Склады отправления и получения должны различаться.");
            return false;
        }

        movement = new Movement
        {
            Type = type,
            FromWarehouseId = needsSource ? source!.Id : null,
            ToWarehouseId = needsTarget ? target!.Id : null,
            CreatedAt = DateTime.Now,
            Comment = string.IsNullOrWhiteSpace(CommentBox.Text) ? null : CommentBox.Text.Trim(),
            Items = _lines
                .Select(line => new MovementItem { ItemId = line.ItemId, Quantity = line.Quantity })
                .ToList()
        };

        return true;
    }

    private string BuildConfirmationText(string type)
    {
        string sourceName = (SourceWarehouse.SelectedItem as Warehouse)?.Name ?? "—";
        string targetName = (TargetWarehouse.SelectedItem as Warehouse)?.Name ?? "—";

        string action = type switch
        {
            MovementTypes.In => $"Провести приход на склад «{targetName}»?",
            MovementTypes.Out => $"Провести расход со склада «{sourceName}»?",
            _ => $"Переместить товары со склада «{sourceName}» на «{targetName}»?"
        };

        int totalQuantity = _lines.Sum(line => line.Quantity);
        return $"{action}\n\nПозиций: {_lines.Count}, единиц: {totalQuantity}.\n" +
               "Документ изменит остатки и будет записан в журнал.";
    }

    /// <summary>
    /// Умная подсказка: предлагаем подставить доступный остаток
    /// вместо сухого запрета операции.
    /// </summary>
    private void HandleShortage(InsufficientStockException shortage)
    {
        string question = shortage.Available > 0
            ? $"Подставить доступный максимум — {shortage.Available}?"
            : "Доступного остатка нет. Удалить позицию из документа?";

        string message = $"Недостаточно товара «{shortage.ItemName}».\n" +
                         $"Доступно: {shortage.Available}, запрошено: {shortage.Requested}.\n\n{question}";

        MessageBoxResult answer = MessageBox.Show(
            message,
            "Нехватка остатка",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        MovementLineModel? line = _lines.FirstOrDefault(existing => existing.Name == shortage.ItemName);
        if (line is null)
            return;

        if (shortage.Available > 0)
        {
            line.Quantity = shortage.Available;
            MessageBox.Show(
                $"Количество обновлено до доступного максимума ({shortage.Available}). Проверьте документ.",
                "Подсказка EasyKeep",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        RemoveLine(line);
        MessageBox.Show(
            "Позиция удалена из документа: на складе нет доступного остатка.",
            "Подсказка EasyKeep",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    // ==================== Авто-балансировка ====================

    private async void AutoBalanceButton_Click(object sender, RoutedEventArgs e)
    {
        List<AutoBalanceDraft> drafts;

        try
        {
            using EasyKeepContext context = new();
            drafts = await new AutoBalanceService(context).BuildDraftsAsync();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Не удалось выполнить авто-балансировку. Проверьте подключение к базе данных.",
                "Авто-балансировка",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (drafts.Count == 0)
        {
            MessageBox.Show(
                "Авто-балансировка не требуется: критичного дефицита на торговых точках нет.",
                "Авто-балансировка",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        List<AutoBalancePreviewRow> preview = await BuildDraftPreviewAsync(drafts);
        AutoBalanceDialog dialog = new(preview);
        Window? owner = Window.GetWindow(this);
        if (owner is not null)
            dialog.Owner = owner;

        if (dialog.ShowDialog() != true)
            return;

        await PostDraftsAsync(drafts);
    }

    /// <summary>Готовит строки предпросмотра с SKU и названиями товаров.</summary>
    private static async Task<List<AutoBalancePreviewRow>> BuildDraftPreviewAsync(List<AutoBalanceDraft> drafts)
    {
        int[] itemIds = drafts
            .SelectMany(draft => draft.Movement.Items)
            .Select(line => line.ItemId)
            .Distinct()
            .ToArray();

        Dictionary<int, Item> items;
        using (EasyKeepContext context = new())
        {
            items = (await new MovementService(context).GetItemsAsync(itemIds))
                .ToDictionary(item => item.Id);
        }

        return drafts
            .SelectMany(draft => draft.Movement.Items.Select(line =>
            {
                Item? item = items.GetValueOrDefault(line.ItemId);
                return new AutoBalancePreviewRow(
                    draft.ToWarehouseName,
                    item?.Sku ?? "—",
                    item?.Name ?? $"Товар #{line.ItemId}",
                    line.Quantity);
            }))
            .ToList();
    }

    private async Task PostDraftsAsync(List<AutoBalanceDraft> drafts)
    {
        int postedCount = 0;
        List<string> failures = new();

        foreach (AutoBalanceDraft draft in drafts)
        {
            try
            {
                using EasyKeepContext context = new();
                await new MovementService(context).PostMovementAsync(draft.Movement);
                postedCount++;
            }
            catch (Exception ex)
            {
                failures.Add($"«{draft.ToWarehouseName}»: {ex.Message}");
            }
        }

        string message = $"Проведено документов: {postedCount} из {drafts.Count}.";
        if (failures.Count > 0)
            message += "\n\nНе удалось провести:\n" + string.Join("\n", failures);

        MessageBox.Show(
            message,
            "Авто-балансировка завершена",
            MessageBoxButton.OK,
            failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

        LoadJournal();
    }

    // ==================== Журнал операций ====================

    private void LoadJournal()
    {
        DateTime from = FromDatePicker.SelectedDate?.Date ?? DateTime.Today.AddDays(-30);
        DateTime to = (ToDatePicker.SelectedDate?.Date ?? DateTime.Today).AddDays(1);

        if (to <= from)
        {
            JournalGrid.ItemsSource = null;
            JournalSummaryText.Text = "Период задан неверно";
            JournalStateText.Text = "Дата начала должна быть раньше даты окончания.";
            JournalStateText.Visibility = Visibility.Visible;
            return;
        }

        string? type = (TypeFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrWhiteSpace(type))
            type = null;

        int? warehouseId = (JournalWarehouse.SelectedItem as Warehouse)?.Id;
        if (warehouseId == 0)
            warehouseId = null;

        try
        {
            using EasyKeepContext context = new();
            List<MovementJournalRow> rows = new MovementService(context)
                .GetJournal(from, to, type, warehouseId, JournalSearchBox.Text);

            JournalGrid.ItemsSource = rows;
            JournalSummaryText.Text = $"Документов: {rows.Count}";
            JournalStateText.Text = "За выбранный период ничего не найдено. Измените фильтры.";
            JournalStateText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception)
        {
            ShowConnectionError();
        }
    }

    private void JournalFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady || !IsLoaded)
            return;

        LoadJournal();
    }

    private void JournalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        LoadJournal();
    }

    private void RefreshJournalButton_Click(object sender, RoutedEventArgs e)
    {
        LoadJournal();
    }

    // ==================== Вспомогательные методы ====================

    private string GetSelectedType()
    {
        return (TypeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? MovementTypes.In;
    }

    private void ShowConnectionError()
    {
        JournalGrid.ItemsSource = null;
        JournalSummaryText.Text = "Нет данных";
        JournalStateText.Text = "Не удалось загрузить журнал. Проверьте подключение к базе данных.";
        JournalStateText.Visibility = Visibility.Visible;
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "Проверьте документ", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
