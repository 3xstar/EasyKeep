using System.Windows;
using System.Windows.Media.Animation;
using EasyKeep.Services;

namespace EasyKeep;

/// <summary>
/// Точка входа: до создания главного окна применяет сохранённую тему
/// и язык, чтобы при запуске не было вспышки светлого оформления
/// и русского текста у англоязычного пользователя.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Все анимации идут на 120 Гц: переходы остаются плавными даже
        // на 120-герцовых экранах и не пропускают кадры при скролле.
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline),
            new FrameworkPropertyMetadata { DefaultValue = 120 });

        UserPreferences preferences = PreferencesService.Load();
        ThemeService.Apply(ThemeService.Parse(preferences.Theme));
        LocalizationManager.Apply(LocalizationManager.Parse(preferences.Language));

        base.OnStartup(e);
    }
}
