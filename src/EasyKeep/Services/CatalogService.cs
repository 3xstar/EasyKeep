using EasyKeep.Data;
using EasyKeep.Data.Entities;
using EasyKeep.Data.Reports;
using Microsoft.EntityFrameworkCore;

namespace EasyKeep.Services;

/// <summary>
/// Каталог товаров и категорий: чтение, создание, изменение и удаление.
/// Вся работа с БД выполняется здесь, интерфейс только вызывает методы.
/// </summary>
public class CatalogService
{
    private readonly EasyKeepContext _context;

    public CatalogService(EasyKeepContext context)
    {
        _context = context;
    }

    public async Task<List<Item>> GetItemsAsync()
    {
        return await _context.Items
            .AsNoTracking()
            .Include(item => item.Category)
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync();
    }

    public async Task<Item> CreateItemAsync(
        string sku,
        string name,
        int categoryId,
        string unit,
        decimal price,
        int minStock)
    {
        Item item = new()
        {
            Sku = NormalizeSku(sku),
            Name = NormalizeRequired(name, "Название товара", 200),
            CategoryId = categoryId,
            Unit = NormalizeRequired(unit, "Единица измерения", 20),
            Price = ValidatePrice(price),
            MinStock = ValidateMinStock(minStock)
        };

        await EnsureSkuIsFreeAsync(item.Sku, null);
        await EnsureCategoryExistsAsync(categoryId);

        _context.Items.Add(item);
        await SaveChangesAsync();
        return item;
    }

    public async Task<Item> UpdateItemAsync(
        int id,
        string sku,
        string name,
        int categoryId,
        string unit,
        decimal price,
        int minStock)
    {
        Item item = await FindItemAsync(id);

        item.Sku = NormalizeSku(sku);
        item.Name = NormalizeRequired(name, "Название товара", 200);
        item.CategoryId = categoryId;
        item.Unit = NormalizeRequired(unit, "Единица измерения", 20);
        item.Price = ValidatePrice(price);
        item.MinStock = ValidateMinStock(minStock);

        await EnsureSkuIsFreeAsync(item.Sku, id);
        await EnsureCategoryExistsAsync(categoryId);

        await SaveChangesAsync();
        return item;
    }

    public async Task DeleteItemAsync(int id)
    {
        Item item = await FindItemAsync(id);

        bool usedInStocks = await _context.Stocks.AnyAsync(stock => stock.ItemId == id);
        bool usedInMovements = await _context.MovementItems.AnyAsync(line => line.ItemId == id);

        if (usedInStocks || usedInMovements)
        {
            throw new InvalidOperationException(
                "Нельзя удалить товар: он используется в остатках или движениях. " +
                "Сначала списывайте или переместите его.");
        }

        _context.Items.Remove(item);
        await SaveChangesAsync();
    }

    public async Task<Category> CreateCategoryAsync(string name)
    {
        string cleanName = NormalizeRequired(name, "Название категории", 100);
        await EnsureCategoryNameIsFreeAsync(cleanName, null);

        Category category = new() { Name = cleanName };
        _context.Categories.Add(category);
        await SaveChangesAsync();
        return category;
    }

    public async Task<Category> UpdateCategoryAsync(int id, string name)
    {
        Category category = await _context.Categories.FindAsync(id)
            ?? throw new InvalidOperationException("Категория не найдена.");

        string cleanName = NormalizeRequired(name, "Название категории", 100);
        await EnsureCategoryNameIsFreeAsync(cleanName, id);

        category.Name = cleanName;
        await SaveChangesAsync();
        return category;
    }

    public async Task DeleteCategoryAsync(int id)
    {
        Category category = await _context.Categories.FindAsync(id)
            ?? throw new InvalidOperationException("Категория не найдена.");

        bool hasItems = await _context.Items.AnyAsync(item => item.CategoryId == id);
        if (hasItems)
        {
            throw new InvalidOperationException(
                "Нельзя удалить категорию, пока в ней есть товары. " +
                "Сначала перенесите товары в другую категорию.");
        }

        _context.Categories.Remove(category);
        await SaveChangesAsync();
    }

    /// <summary>История движений товара для панели «Рентген товара».</summary>
    public List<ItemMovementRow> GetItemHistory(int itemId, int limit = 50)
    {
        return new AdoNetMovementRepository().GetItemHistory(itemId, limit);
    }

    private async Task<Item> FindItemAsync(int id)
    {
        return await _context.Items.FindAsync(id)
               ?? throw new InvalidOperationException("Товар не найден.");
    }

    private async Task EnsureSkuIsFreeAsync(string sku, int? excludeId)
    {
        bool taken = await _context.Items.AnyAsync(item =>
            item.Sku == sku && (excludeId == null || item.Id != excludeId.Value));

        if (taken)
            throw new InvalidOperationException($"Товар с SKU «{sku}» уже существует.");
    }

    private async Task EnsureCategoryExistsAsync(int categoryId)
    {
        bool exists = await _context.Categories.AnyAsync(category => category.Id == categoryId);
        if (!exists)
            throw new InvalidOperationException("Выберите существующую категорию.");
    }

    private async Task EnsureCategoryNameIsFreeAsync(string name, int? excludeId)
    {
        bool taken = await _context.Categories.AnyAsync(category =>
            category.Name == name && (excludeId == null || category.Id != excludeId.Value));

        if (taken)
            throw new InvalidOperationException($"Категория «{name}» уже существует.");
    }

    private async Task SaveChangesAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Не удалось сохранить изменения. Проверьте заполнение полей и связи данных.", ex);
        }
    }

    private static string NormalizeSku(string sku)
    {
        return NormalizeRequired(sku, "SKU", 50).ToUpperInvariant();
    }

    private static string NormalizeRequired(string value, string fieldName, int maxLength)
    {
        string cleanValue = value.Trim();

        if (cleanValue.Length == 0)
            throw new InvalidOperationException($"Поле «{fieldName}» не должно быть пустым.");

        if (cleanValue.Length > maxLength)
            throw new InvalidOperationException($"Поле «{fieldName}» длиннее {maxLength} символов.");

        return cleanValue;
    }

    private static decimal ValidatePrice(decimal price)
    {
        if (price < 0)
            throw new InvalidOperationException("Цена не может быть отрицательной.");

        return price;
    }

    private static int ValidateMinStock(int minStock)
    {
        if (minStock < 0)
            throw new InvalidOperationException("Минимальный остаток не может быть отрицательным.");

        return minStock;
    }
}
