using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Главное окно EasyKeep: боковое меню, рабочая область экранов
/// и зона персонализации (тема + масштаб интерфейса).
/// </summary>
public partial class MainWindow : Window
{
    private static readonly int[] ScaleSteps = { 85, 95, 100, 110, 120, 130, 140 };
    private const double ExpandedNavWidth = 248;
    private const double CompactNavWidth = 64;

    private int _scalePercent = 100;
    private bool _isInitializing;
    private bool _isNavCompact;
    private string _currentSection = "stocks";

    // Экраны создаются один раз: парсинг XAML и построение визуального
    // дерева больше не повторяются при каждом переключении вкладки.
    private readonly Dictionary<string, UserControl> _screens = new();

    public MainWindow()
    {
        InitializeComponent();

        _isInitializing = true;

        UserPreferences preferences = PreferencesService.Load();
        AppTheme theme = ThemeService.Parse(preferences.Theme);

        DarkThemeOption.IsChecked = theme == AppTheme.Dark;
        LightThemeOption.IsChecked = theme == AppTheme.Light;
        ApplyScale(preferences.ScalePercent, save: false);

        // Язык уже применён в App.OnStartup — выставляем переключатель.
        AppLanguage language = LocalizationManager.Current;
        RuLanguageOption.IsChecked = language == AppLanguage.Ru;
        EnLanguageOption.IsChecked = language == AppLanguage.En;
        LocalizationManager.ApplyCulture(this);

        _isInitializing = false;

        AnimateWindowEntrance();

        // По умолчанию открываем самый важный раздел — остатки.
        StocksNav.IsChecked = true;
    }

    /// <summary>
    /// Плавное проявление окна при старте: 220 мс CubicEase EaseOut
    /// и лёгкий подъём контейнера 10 px → 0 за 240 мс, без отскока.
    /// Анимируются только GPU-свойства (Opacity, RenderTransform).
    /// </summary>
    private void AnimateWindowEntrance()
    {
        Opacity = 0;
        WindowRise.Y = 10;

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });

        WindowRise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    /// <summary>Переключает экран по пункту меню.</summary>
    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string section })
            return;

        ShowSection(section);
        MoveActivePill(sender as RadioButton, animate: true);
    }

    private void ShowSection(string section)
    {
        _currentSection = section;
        ApplySectionTitle(section);

        bool isCached = _screens.TryGetValue(section, out UserControl? view);

        if (!isCached)
        {
            view = CreateSectionView(section);

            if (view is not null)
                _screens[section] = view;
        }

        if (view is null)
        {
            // Остальные экраны подключаются отдельными шагами.
            CurrentViewHost.Content = null;
            PreviousViewHost.Content = null;
            PreviousViewHost.Opacity = 0;
            CurrentViewHost.Visibility = Visibility.Collapsed;
            PlaceholderPanel.Visibility = Visibility.Visible;
            PlaceholderTitle.Text = PageTitleText.Text;
            return;
        }

        PlaceholderPanel.Visibility = Visibility.Collapsed;

        // Быстрое переключение туда-обратно: экран ещё затухает в
        // PreviousViewHost — забираем его оттуда, чтобы не показать дважды.
        if (ReferenceEquals(PreviousViewHost.Content, view))
        {
            PreviousViewHost.BeginAnimation(OpacityProperty, null);
            PreviousViewHost.Opacity = 0;
            PreviousViewHost.Content = null;
        }

        // Старый экран уходит на верхний слой и растворяется, новый
        // занимает рабочую область и проявляется снизу.
        object? outgoing = CurrentViewHost.Content;
        if (outgoing is not null)
        {
            CurrentViewHost.Content = null;
            PreviousViewHost.BeginAnimation(OpacityProperty, null);
            PreviousViewHost.Opacity = 1;
            PreviousViewHost.Content = outgoing;
        }

        CurrentViewHost.BeginAnimation(OpacityProperty, null);
        CurrentViewHost.Content = view;
        CurrentViewHost.Visibility = Visibility.Visible;
        CurrentViewHost.Opacity = 0;

        ContentShift.BeginAnimation(TranslateTransform.YProperty, null);
        ContentShift.Y = 10;

        // Повторный вход: визуальное дерево уже готово, а данные
        // обновляются в фоне — анимация не ждёт запрос к БД.
        if (isCached && view is IRefreshableScreen refreshable)
            _ = refreshable.RefreshAsync();

        // Кадр с новым экраном отрисовывается до старта анимации:
        // переход начинается без рывка и без пустого кадра.
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(AnimateSectionChange));
    }

    /// <summary>
    /// Двухслойный переход: старый экран гаснет за 180 мс (CubicEase
    /// EaseOut), новый проявляется и выезжает снизу за 220 мс
    /// (QuinticEase EaseOut). Анимируются только GPU-свойства.
    /// </summary>
    private void AnimateSectionChange()
    {
        CurrentViewHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        });

        ContentShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
        });

        if (PreviousViewHost.Content is not { } fading)
            return;

        DoubleAnimation fadeOut = new(1, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        fadeOut.Completed += (_, _) =>
        {
            // За время анимации пользователь мог переключиться ещё раз.
            if (!ReferenceEquals(PreviousViewHost.Content, fading))
                return;

            PreviousViewHost.Content = null;
            PreviousViewHost.BeginAnimation(OpacityProperty, null);
            PreviousViewHost.Opacity = 0;
        };

        PreviousViewHost.BeginAnimation(OpacityProperty, fadeOut);
    }

    /// <summary>
    /// Скользящая капсула активного пункта: 200 мс QuinticEase EaseOut.
    /// Позиция берётся из реального расположения пункта в меню, поэтому
    /// капсула остаётся точной при любом масштабе интерфейса.
    /// </summary>
    private void MoveActivePill(RadioButton? item, bool animate)
    {
        if (item is null)
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (item.IsChecked != true || !item.IsLoaded)
                return;

            GeneralTransform transform = item.TransformToAncestor(NavItemsHost);
            Point origin = transform.Transform(new Point(0, 0));

            NavActivePill.Height = item.ActualHeight;

            DoubleAnimation slide = new(NavActivePillShift.Y, origin.Y, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };

            if (animate)
            {
                NavActivePillShift.BeginAnimation(TranslateTransform.YProperty, slide);
            }
            else
            {
                NavActivePillShift.BeginAnimation(TranslateTransform.YProperty, null);
                NavActivePillShift.Y = origin.Y;
            }
        }));
    }

    private static UserControl? CreateSectionView(string section) => section switch
    {
        "stocks" => new StocksView(),
        "movements" => new MovementsView(),
        "catalog" => new CatalogView(),
        "reports" => new ReportsView(),
        _ => null
    };

    /// <summary>Заголовок и подзаголовок раздела — по текущему языку.</summary>
    private void ApplySectionTitle(string section)
    {
        (string titleKey, string subtitleKey) = SectionKeys(section);
        PageTitleText.Text = LocalizationManager.Get(titleKey);
        PageSubtitleText.Text = LocalizationManager.Get(subtitleKey);
    }

    private static (string TitleKey, string SubtitleKey) SectionKeys(string section) => section switch
    {
        "movements" => ("Section_Movements_Title", "Section_Movements_Subtitle"),
        "catalog" => ("Section_Catalog_Title", "Section_Catalog_Subtitle"),
        "reports" => ("Section_Reports_Title", "Section_Reports_Subtitle"),
        _ => ("Section_Stocks_Title", "Section_Stocks_Subtitle")
    };

    // ==================== Персонализация ====================

    /// <summary>
    /// Компактный режим навигации: 248 → 64 px. Освободившиеся пиксели
    /// мгновенно получает рабочая область; подписи скрываются, остаются
    /// крупные центрированные иконки с подсказками.
    /// </summary>
    private void NavToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyNavMode(!_isNavCompact);
    }

    private void ApplyNavMode(bool compact)
    {
        _isNavCompact = compact;

        // Ширина колонки анимируется плавно (в WPF нет встроенной
        // анимации GridLength — см. GridLengthAnimation); остальное
        // содержимое меню переключается мгновенно.
        NavColumn.BeginAnimation(
            ColumnDefinition.WidthProperty,
            new GridLengthAnimation
            {
                From = NavColumn.Width,
                To = new GridLength(compact ? CompactNavWidth : ExpandedNavWidth),
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            });

        // Высота пунктов не меняется, но после смены ширины колонки
        // пересчитываем капсулу на тот же Loaded-приоритет.
        MoveActivePill(GetCheckedNavItem(), animate: false);

        NavPanel.Margin = compact
            ? new Thickness(8, 18, 8, 14)
            : new Thickness(16, 22, 16, 16);

        Visibility textVisibility = compact ? Visibility.Collapsed : Visibility.Visible;
        BrandText.Visibility = textVisibility;
        SectionsHeaderText.Visibility = textVisibility;
        DatabaseStatusCard.Visibility = textVisibility;
        PersonalizationLabel.Visibility = textVisibility;
        StocksNavText.Visibility = textVisibility;
        MovementsNavText.Visibility = textVisibility;
        CatalogNavText.Visibility = textVisibility;
        ReportsNavText.Visibility = textVisibility;

        LogoPanel.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        LogoImage.Width = compact ? 32 : 40;
        LogoImage.Height = compact ? 32 : 40;

        NavToggleRotation.Angle = compact ? 180 : 0;
        NavToggleButton.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        Grid.SetColumnSpan(NavToggleButton, compact ? 2 : 1);

        HorizontalAlignment navContentAlignment = compact
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Stretch;
        StocksNav.HorizontalContentAlignment = navContentAlignment;
        MovementsNav.HorizontalContentAlignment = navContentAlignment;
        CatalogNav.HorizontalContentAlignment = navContentAlignment;
        ReportsNav.HorizontalContentAlignment = navContentAlignment;

        PersonalizationButton.HorizontalContentAlignment = compact
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;

        // В компактном меню попап раскрывается вправо, чтобы не уехать за край окна.
        PersonalizationPopup.Placement = compact ? PlacementMode.Right : PlacementMode.Top;
        PersonalizationPopup.HorizontalOffset = compact ? 8 : 0;
        PersonalizationPopup.VerticalOffset = compact ? 0 : -8;
    }

    private void PersonalizationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PersonalizationPopup.IsOpen)
            ResetPersonalizationPopupAnimation();

        PersonalizationPopup.IsOpen = !PersonalizationPopup.IsOpen;
    }

    /// <summary>Сбрасывает капсулу попапа перед открытием: масштаб 0.95, прозрачность 0.</summary>
    private void ResetPersonalizationPopupAnimation()
    {
        PersonalizationCard.BeginAnimation(OpacityProperty, null);
        PersonalizationScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PersonalizationScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        PersonalizationCard.Opacity = 0;
        PersonalizationScale.ScaleX = 0.95;
        PersonalizationScale.ScaleY = 0.95;
    }

    /// <summary>Попап персонализации проявляется и мягко увеличивается за 160 мс.</summary>
    private void PersonalizationPopup_Opened(object? sender, EventArgs e)
    {
        QuinticEase easing = new() { EasingMode = EasingMode.EaseOut };
        TimeSpan duration = TimeSpan.FromMilliseconds(160);

        PersonalizationCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = easing
        });

        PersonalizationScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.95, 1, duration)
        {
            EasingFunction = easing
        });

        PersonalizationScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.95, 1, duration)
        {
            EasingFunction = easing
        });
    }

    private void ThemeOption_Checked(object sender, RoutedEventArgs e)
    {
        if (LightThemeOption is null || DarkThemeOption is null)
            return;

        AppTheme theme = DarkThemeOption.IsChecked == true ? AppTheme.Dark : AppTheme.Light;
        ThemeService.Apply(theme);

        if (!_isInitializing)
            SavePreferences();
    }

    private void LanguageOption_Checked(object sender, RoutedEventArgs e)
    {
        if (RuLanguageOption is null || EnLanguageOption is null)
            return;

        AppLanguage language = EnLanguageOption.IsChecked == true ? AppLanguage.En : AppLanguage.Ru;
        if (language == LocalizationManager.Current)
            return;

        // Словарь строк подменяется мгновенно: весь XAML переключается
        // сам через DynamicResource, календари — через Culture.
        LocalizationManager.Apply(language);
        LocalizationManager.ApplyCulture(this);
        ApplySectionTitle(_currentSection);

        if (!_isInitializing)
            SavePreferences();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeScale(1);
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeScale(-1);
    }

    private void ZoomResetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyScale(100, save: true);
    }

    /// <summary>Ctrl + / Ctrl − / Ctrl 0 — привычное управление масштабом.</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && PersonalizationPopup.IsOpen)
        {
            PersonalizationPopup.IsOpen = false;
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                ChangeScale(1);
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ChangeScale(-1);
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                ApplyScale(100, save: true);
                e.Handled = true;
                break;
        }
    }

    private void ChangeScale(int direction)
    {
        int currentIndex = NearestStepIndex(_scalePercent);
        int nextIndex = Math.Clamp(currentIndex + direction, 0, ScaleSteps.Length - 1);
        ApplyScale(ScaleSteps[nextIndex], save: true);
    }

    private void ApplyScale(int percent, bool save)
    {
        _scalePercent = Math.Clamp(percent, ScaleSteps[0], ScaleSteps[^1]);

        // Адаптивное масштабирование как в браузере: пересчитываем базовые
        // ресурсы — шрифты, высоты контролов и отступы карточек. Layout с
        // *-колонками сам ужимается под окно, без ScaleTransform (мыла) и
        // внешнего ScrollViewer (горизонтальных полос).
        double factor = _scalePercent / 100.0;
        ResourceDictionary resources = Application.Current.Resources;
        resources["FontSizeSmall"] = 11.0 * factor;
        resources["FontSizeBase"] = 13.0 * factor;
        resources["FontSizeTitle"] = 15.0 * factor;
        resources["FontSizeSubtitle"] = 20.0 * factor;
        resources["FontSizeBrand"] = 18.0 * factor;
        resources["FontSizePageTitle"] = 24.0 * factor;
        resources["FontSizeKpi"] = 30.0 * factor;
        resources["ControlHeight"] = 36.0 * factor;
        resources["TableRowHeight"] = 44.0 * factor;
        resources["CardPadding"] = new Thickness(20.0 * factor);
        resources["TableHintMargin"] = new Thickness(16.0 * factor, 44.0 * factor, 16.0 * factor, 16.0 * factor);
        ScalePercentText.Text = $"{_scalePercent}%";

        // Высоты пунктов меню зависят от шрифта — возвращаем капсулу на место.
        MoveActivePill(GetCheckedNavItem(), animate: false);

        if (save)
            SavePreferences();
    }

    /// <summary>Текущий выбранный пункт меню — для позиционирования капсулы.</summary>
    private RadioButton? GetCheckedNavItem()
    {
        if (StocksNav.IsChecked == true)
            return StocksNav;
        if (MovementsNav.IsChecked == true)
            return MovementsNav;
        if (CatalogNav.IsChecked == true)
            return CatalogNav;
        if (ReportsNav.IsChecked == true)
            return ReportsNav;

        return null;
    }

    private static int NearestStepIndex(int percent)
    {
        int bestIndex = 0;
        for (int index = 1; index < ScaleSteps.Length; index++)
        {
            if (Math.Abs(ScaleSteps[index] - percent) < Math.Abs(ScaleSteps[bestIndex] - percent))
                bestIndex = index;
        }

        return bestIndex;
    }

    private void SavePreferences()
    {
        PreferencesService.Save(new UserPreferences
        {
            Theme = ThemeService.Current.ToString(),
            Language = LocalizationManager.Current == AppLanguage.En ? "en" : "ru",
            ScalePercent = _scalePercent
        });
    }
}
