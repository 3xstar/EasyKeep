using System.IO;
using System.Text.Json;

namespace EasyKeep.Data;

/// <summary>
/// Загружает строку подключения из локального файла или общей конфигурации.
/// Реальные секреты хранятся только в appsettings.Local.json (файл в .gitignore).
/// </summary>
public static class AppConfig
{
    private const string LocalFileName = "appsettings.Local.json";
    private const string SharedFileName = "appsettings.json";

    // Запасной вариант для учебного запуска: локальная база разработчика.
    private const string FallbackConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=EasyKeep;Trusted_Connection=True;TrustServerCertificate=True;";

    public static string GetConnectionString()
    {
        // Сначала пробуем локальный файл, затем общую конфигурацию.
        string? localValue = ReadConnectionString(LocalFileName);
        if (!string.IsNullOrWhiteSpace(localValue))
            return localValue;

        string? sharedValue = ReadConnectionString(SharedFileName);
        if (!string.IsNullOrWhiteSpace(sharedValue))
            return sharedValue;

        // Конфиг не найден или пуст — подключаемся к стандартной LocalDB.
        return FallbackConnectionString;
    }

    private static string? ReadConnectionString(string fileName)
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(filePath))
            return null;

        // Разрешаем комментарии и запятые в конце — так конфиг проще читать.
        JsonDocumentOptions options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(filePath), options);
        if (document.RootElement.TryGetProperty("ConnectionStrings", out JsonElement section) &&
            section.TryGetProperty("EasyKeep", out JsonElement value))
        {
            return value.GetString();
        }

        return null;
    }
}
