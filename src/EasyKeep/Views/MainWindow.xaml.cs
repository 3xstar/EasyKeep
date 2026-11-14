using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        _currentSection = section;
        ApplySectionTitle(section);

        UserControl? view = section switch
        {
            "stocks" => new StocksView(),
            "movements" => new MovementsView(),
            "catalog" => new CatalogView(),
            "reports" => new ReportsView(),
            _ => null
        };

        if (view is not null)
        {
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            ContentArea.Content = view;
            ContentArea.Visibility = Visibility.Visible;
            AnimateContentEntrance();
            return;
        }

        // Остальные экраны подключаются отдельными шагами.
        ContentArea.Content = null;
        ContentArea.Visibility = Visibility.Collapsed;
        PlaceholderPanel.Visibility = Visibility.Visible;
        PlaceholderTitle.Text = PageTitleText.Text;
    }

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

    /// <summary>
    /// Мгновенный, но мягкий вход экрана: 120 мс, прозрачность 0.6 → 1
    /// и сдвиг на 4 px вниз с CubicEase EaseOut.
    /// </summary>
    private void AnimateContentEntrance()
    {
        CubicEase easing = new() { EasingMode = EasingMode.EaseOut };
        TimeSpan duration = TimeSpan.FromMilliseconds(120);

        ContentArea.BeginAnimation(OpacityProperty, new DoubleAnimation(0.6, 1, duration)
        {
            EasingFunction = easing
        });

        ContentShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, duration)
        {
            EasingFunction = easing
        });
    }

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

        NavColumn.Width = new GridLength(compact ? CompactNavWidth : ExpandedNavWidth);
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
        PersonalizationPopup.IsOpen = !PersonalizationPopup.IsOpen;
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

        if (save)
            SavePreferences();
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
