using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyKeep.Data.Entities;

/// <summary>Товар (номенклатурная позиция).</summary>
[Table("items")]
public class Item
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("unit")]
    public string Unit { get; set; } = string.Empty;

    [Column("price")]
    public decimal Price { get; set; }

    [Column("min_stock")]
    public int MinStock { get; set; }

    public Category? Category { get; set; }

    public List<Stock> Stocks { get; set; } = new();

    public List<MovementItem> MovementItems { get; set; } = new();
}
