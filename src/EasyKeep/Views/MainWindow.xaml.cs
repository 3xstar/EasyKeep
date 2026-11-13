using System.Windows;
using System.Windows.Controls;
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
    private static readonly int[] ScaleSteps = { 85, 95, 100, 110, 120, 135, 150 };

    private int _scalePercent = 100;
    private bool _isInitializing;

    public MainWindow()
    {
        InitializeComponent();

        _isInitializing = true;

        UserPreferences preferences = PreferencesService.Load();
        AppTheme theme = ThemeService.Parse(preferences.Theme);

        DarkThemeOption.IsChecked = theme == AppTheme.Dark;
        LightThemeOption.IsChecked = theme == AppTheme.Light;
        ApplyScale(preferences.ScalePercent, save: false);

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
        PlaceholderTitle.Text = title;
    }

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

        double factor = _scalePercent / 100.0;
        UiScale.ScaleX = factor;
        UiScale.ScaleY = factor;
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
            ScalePercent = _scalePercent
        });
    }
}
