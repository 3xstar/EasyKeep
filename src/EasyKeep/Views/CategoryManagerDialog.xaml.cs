using System.Windows;
using System.Windows.Controls;
using EasyKeep.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Services;

namespace EasyKeep.Views;

/// <summary>
/// Управление категориями: добавление, переименование и удаление.
/// </summary>
public partial class CategoryManagerDialog : Window
{
    public CategoryManagerDialog()
    {
        InitializeComponent();
    }

    private async void CategoryManagerDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync(int? selectId = null)
    {
        try
        {
            List<Category> categories;
            using (EasyKeepContext context = new())
            {
                categories = await new CatalogService(context).GetCategoriesAsync();
            }

            CategoriesList.ItemsSource = categories;
            CategoryCountText.Text = $"Категорий: {categories.Count}";

            if (selectId is not null)
                CategoriesList.SelectedItem = categories.FirstOrDefault(category => category.Id == selectId);
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Не удалось загрузить категории. Проверьте подключение к базе данных.",
                "Категории",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowWarning("Введите название новой категории.");
            return;
        }

        try
        {
            using EasyKeepContext context = new();
            Category created = await new CatalogService(context).CreateCategoryAsync(name);
            NameBox.Clear();
            await LoadCategoriesAsync(created.Id);
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception)
        {
            ShowWarning("Не удалось добавить категорию. Проверьте подключение к базе данных.");
        }
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesList.SelectedItem is not Category category)
        {
            ShowWarning("Выберите категорию в списке.");
            return;
        }

        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowWarning("Введите новое название категории.");
            return;
        }

        try
        {
            using EasyKeepContext context = new();
            await new CatalogService(context).UpdateCategoryAsync(category.Id, name);
            await LoadCategoriesAsync(category.Id);
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception)
        {
            ShowWarning("Не удалось переименовать категорию. Проверьте подключение к базе данных.");
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesList.SelectedItem is not Category category)
        {
            ShowWarning("Выберите категорию в списке.");
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            $"Удалить категорию «{category.Name}»?\nЭто действие необратимо.",
            "Подтверждение удаления",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            using EasyKeepContext context = new();
            await new CatalogService(context).DeleteCategoryAsync(category.Id);
            NameBox.Clear();
            await LoadCategoriesAsync();
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
        catch (Exception)
        {
            ShowWarning("Не удалось удалить категорию. Проверьте подключение к базе данных.");
        }
    }

    private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoriesList.SelectedItem is Category category)
            NameBox.Text = category.Name;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void ShowWarning(string message)
    {
        MessageBox.Show(message, "Категории", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
