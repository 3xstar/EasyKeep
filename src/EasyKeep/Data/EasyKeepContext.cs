using EasyKeep.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyKeep.Data;

/// <summary>
/// EF Core контекст для справочников, остатков и аналитики.
/// Проведение движений выполняется отдельно через ADO.NET (AdoNetMovementRepository).
/// </summary>
public class EasyKeepContext : DbContext
{
    public EasyKeepContext()
    {
    }

    public EasyKeepContext(DbContextOptions<EasyKeepContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Movement> Movements => Set<Movement>();
    public DbSet<MovementItem> MovementItems => Set<MovementItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Если контекст создан вручную, берём строку подключения из appsettings.
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer(AppConfig.GetConnectionString());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Уникальные ключи и индексы повторяют ограничения database/schema.sql.
        modelBuilder.Entity<Category>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<Item>()
            .HasIndex(i => i.Sku)
            .IsUnique();

        modelBuilder.Entity<Item>()
            .HasIndex(i => i.Name);

        modelBuilder.Entity<Warehouse>()
            .HasIndex(w => w.Name)
            .IsUnique();

        modelBuilder.Entity<Stock>()
            .HasIndex(s => new { s.WarehouseId, s.ItemId })
            .IsUnique();

        modelBuilder.Entity<Movement>()
            .HasIndex(m => m.CreatedAt);

        modelBuilder.Entity<Movement>()
            .HasIndex(m => m.Type);

        modelBuilder.Entity<MovementItem>()
            .HasIndex(mi => new { mi.MovementId, mi.ItemId })
            .IsUnique();

        modelBuilder.Entity<MovementItem>()
            .HasIndex(mi => mi.ItemId);

        ConfigureRelationships(modelBuilder);
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // Товар принадлежит категории; удаление используемой категории запрещено.
        modelBuilder.Entity<Item>()
            .HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Остаток ссылается на склад и товар.
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Warehouse)
            .WithMany(w => w.Stocks)
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Item)
            .WithMany(i => i.Stocks)
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // У движения два разных склада, поэтому связи настраиваются явно.
        modelBuilder.Entity<Movement>()
            .HasOne(m => m.FromWarehouse)
            .WithMany()
            .HasForeignKey(m => m.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Movement>()
            .HasOne(m => m.ToWarehouse)
            .WithMany()
            .HasForeignKey(m => m.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Строки документа удаляются вместе с шапкой (как ON DELETE CASCADE в БД).
        modelBuilder.Entity<MovementItem>()
            .HasOne(mi => mi.Movement)
            .WithMany(m => m.Items)
            .HasForeignKey(mi => mi.MovementId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MovementItem>()
            .HasOne(mi => mi.Item)
            .WithMany(i => i.MovementItems)
            .HasForeignKey(mi => mi.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
