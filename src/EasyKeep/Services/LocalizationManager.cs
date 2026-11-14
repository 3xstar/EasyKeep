using System.Windows;
using System.Windows.Markup;

namespace EasyKeep.Services;

public enum AppLanguage
{
    Ru,
    En
}

/// <summary>
/// Переключение языка интерфейса без перезапуска: подменяет словарь
/// строковых ресурсов, а элементы обновляются через DynamicResource.
/// Программные строки (сводки, диалоги, заглушки) читаются через Get(...)
/// из того же словаря и пересчитываются по событию LanguageChanged.
/// </summary>
public static class LocalizationManager
{
    private const string RuSource = "Localization/Strings.ru.xaml";
    private const string EnSource = "Localization/Strings.en.xaml";

    public static AppLanguage Current { get; private set; } = AppLanguage.Ru;

    /// <summary>Сигнал экранам и диалогам пересчитать строки, собранные в коде.</summary>
    public static event EventHandler? LanguageChanged;

    public static void Apply(AppLanguage language)
    {
        if (Application.Current is null)
            return;

        Current = language;
        string source = language == AppLanguage.En ? EnSource : RuSource;
        ResourceDictionary replacement = new() { Source = new Uri(source, UriKind.Relative) };

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (int index = 0; index < dictionaries.Count; index++)
        {
            string? currentSource = dictionaries[index].Source?.OriginalString;
            if (currentSource is not null &&
                currentSource.Contains("Strings", StringComparison.OrdinalIgnoreCase))
            {
                dictionaries[index] = replacement;
                LanguageChanged?.Invoke(null, EventArgs.Empty);
                return;
            }
        }

        dictionaries.Add(replacement);
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Программная строка по ключу без префикса Loc_.</summary>
    public static string Get(string key)
    {
        return Application.Current?.TryFindResource($"Loc_{key}") as string ?? $"Loc_{key}";
    }

    /// <summary>Календари и форматы дат следуют выбранному языку.</summary>
    public static void ApplyCulture(FrameworkElement element)
    {
        element.Language = XmlLanguage.GetLanguage(Get("PickerCulture"));
    }

    public static AppLanguage Parse(string? value)
    {
        return string.Equals(value, nameof(AppLanguage.En), StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.En
            : AppLanguage.Ru;
    }
}
