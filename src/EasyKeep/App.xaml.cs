using System.Windows;
using EasyKeep.Services;

namespace EasyKeep;

/// <summary>
/// Точка входа: до создания главного окна применяет сохранённую тему,
/// чтобы при запуске не было вспышки светлого оформления.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        UserPreferences preferences = PreferencesService.Load();
        ThemeService.Apply(ThemeService.Parse(preferences.Theme));

        base.OnStartup(e);
    }
}
