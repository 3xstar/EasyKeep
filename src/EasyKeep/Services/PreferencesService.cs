using System.IO;
using System.Text.Json;

namespace EasyKeep.Services;

/// <summary>Настройки внешнего вида, сохраняемые между запусками приложения.</summary>
public sealed class UserPreferences
{
    public string Theme { get; init; } = "Light";
    public string Language { get; init; } = "ru";
    public int ScalePercent { get; init; } = 100;
}

/// <summary>
/// Простое локальное хранилище настроек пользователя в %AppData%\EasyKeep.
/// Ошибки чтения и записи не должны мешать работе приложения.
/// </summary>
public static class PreferencesService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyKeep");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static UserPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserPreferences();

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        catch (Exception)
        {
            return new UserPreferences();
        }
    }

    public static void Save(UserPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(preferences, SerializerOptions));
        }
        catch (Exception)
        {
            // Настройки внешнего вида не критичны: приложение продолжает работу.
        }
    }
}
