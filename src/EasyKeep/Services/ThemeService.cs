using System.Windows;

namespace EasyKeep.Services;

public enum AppTheme
{
    Light,
    Dark
}

/// <summary>
/// Переключение темы без перезапуска: подменяет словарь палитры
/// в ресурсах приложения, а элементы обновляются через DynamicResource.
/// </summary>
public static class ThemeService
{
    private const string LightThemeSource = "Themes/LightTheme.xaml";
    private const string DarkThemeSource = "Themes/DarkTheme.xaml";

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        if (Application.Current is null)
            return;

        Current = theme;
        string source = theme == AppTheme.Dark ? DarkThemeSource : LightThemeSource;
        ResourceDictionary replacement = new() { Source = new Uri(source, UriKind.Relative) };

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (int index = 0; index < dictionaries.Count; index++)
        {
            string? currentSource = dictionaries[index].Source?.OriginalString;
            if (currentSource is not null &&
                currentSource.Contains("Theme", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[index] = replacement;
                return;
            }
        }

        dictionaries.Insert(0, replacement);
    }

    public static AppTheme Parse(string? value)
    {
        return string.Equals(value, nameof(AppTheme.Dark), StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Dark
            : AppTheme.Light;
    }
}
