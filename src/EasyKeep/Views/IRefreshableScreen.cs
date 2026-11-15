namespace EasyKeep.Views;

/// <summary>
/// Экран, который умеет асинхронно перечитать данные при повторном
/// показе. Экземпляры экранов кэшируются в MainWindow, поэтому XAML
/// и визуальное дерево не пересоздаются, а обновляются только данные.
/// </summary>
public interface IRefreshableScreen
{
    Task RefreshAsync();
}
