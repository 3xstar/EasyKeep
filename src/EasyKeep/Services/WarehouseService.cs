using EasyKeep.Data;
using EasyKeep.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyKeep.Services;

/// <summary>
/// Справочник складов для фильтров интерфейса.
/// </summary>
public class WarehouseService
{
    private readonly EasyKeepContext _context;

    public WarehouseService(EasyKeepContext context)
    {
        _context = context;
    }

    /// <summary>Все склады в алфавитном порядке.</summary>
    public async Task<List<Warehouse>> GetWarehousesAsync()
    {
        return await _context.Warehouses
            .AsNoTracking()
            .OrderBy(warehouse => warehouse.Name)
            .ToListAsync();
    }
}
