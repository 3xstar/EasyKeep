using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyKeep.Data.Entities;

/// <summary>Допустимые типы документов движения.</summary>
public static class MovementTypes
{
    public const string In = "IN";
    public const string Out = "OUT";
    public const string Transfer = "TRANSFER";
}

/// <summary>Шапка документа движения товаров.</summary>
[Table("movements")]
public class Movement
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(10)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("from_warehouse_id")]
    public int? FromWarehouseId { get; set; }

    [Column("to_warehouse_id")]
    public int? ToWarehouseId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(500)]
    [Column("comment")]
    public string? Comment { get; set; }

    public Warehouse? FromWarehouse { get; set; }

    public Warehouse? ToWarehouse { get; set; }

    public List<MovementItem> Items { get; set; } = new();
}
