using System.Windows;
using EasyKeep.Data;

namespace EasyKeep.Views;

/// <summary>
/// Стартовое окно-каркас. Показывает, что приложение запустилось
/// и что строка подключения к базе данных настроена.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ShowConfigurationStatus();
    }

    private void ShowConfigurationStatus()
    {
        try
        {
            _ = AppConfig.GetConnectionString();
            ConfigurationStatusText.Text = "Каркас приложения готов. Строка подключения настроена.";
        }
        catch (Exception ex)
        {
            // Показываем понятное сообщение без деталей строки подключения.
            ConfigurationStatusText.Text = ex.Message;
        }
    }
}
