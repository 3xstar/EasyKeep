using System.Windows;
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
        UserPreferences preferences = PreferencesService.Load();
        ThemeService.Apply(ThemeService.Parse(preferences.Theme));
        LocalizationManager.Apply(LocalizationManager.Parse(preferences.Language));

        base.OnStartup(e);
    }
}
