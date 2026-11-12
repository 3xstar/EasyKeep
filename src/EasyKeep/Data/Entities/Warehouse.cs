using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyKeep.Data.Entities;

/// <summary>Склад или торговая точка.</summary>
[Table("warehouses")]
public class Warehouse
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    [Column("address")]
    public string? Address { get; set; }

    public List<Stock> Stocks { get; set; } = new();
}
