using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyKeep.Data.Entities;

/// <summary>Текущий остаток товара на конкретном складе.</summary>
[Table("stocks")]
public class Stock
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("warehouse_id")]
    public int WarehouseId { get; set; }

    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    public Warehouse? Warehouse { get; set; }

    public Item? Item { get; set; }
}
