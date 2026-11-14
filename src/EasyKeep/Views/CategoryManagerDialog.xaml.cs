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

        LocalizationManager.ApplyCulture(this);
        Title = LocalizationManager.Get("CatDialog_Title");
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
            CategoryCountText.Text = string.Format(LocalizationManager.Get("CatDialog_Count"), categories.Count);

            if (selectId is not null)
                CategoriesList.SelectedItem = categories.FirstOrDefault(category => category.Id == selectId);
        }
        catch (Exception)
        {
            MessageBox.Show(
                LocalizationManager.Get("CatDialog_LoadError"),
                LocalizationManager.Get("CatDialog_WarnTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowWarning(LocalizationManager.Get("CatDialog_WarnNewName"));
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
            ShowWarning(LocalizationManager.Get("CatDialog_CreateError"));
        }
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesList.SelectedItem is not Category category)
        {
            ShowWarning(LocalizationManager.Get("CatDialog_WarnSelect"));
            return;
        }

        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowWarning(LocalizationManager.Get("CatDialog_WarnRenameName"));
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
            ShowWarning(LocalizationManager.Get("CatDialog_RenameError"));
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (CategoriesList.SelectedItem is not Category category)
        {
            ShowWarning(LocalizationManager.Get("CatDialog_WarnSelect"));
            return;
        }

        MessageBoxResult answer = MessageBox.Show(
            string.Format(LocalizationManager.Get("CatDialog_DeleteConfirm"), category.Name) + "\n" +
            LocalizationManager.Get("Common_DeleteIrreversible"),
            LocalizationManager.Get("Common_ConfirmDeleteTitle"),
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
            ShowWarning(LocalizationManager.Get("CatDialog_DeleteError"));
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
        MessageBox.Show(message, LocalizationManager.Get("CatDialog_WarnTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
