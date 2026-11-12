using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyKeep.Data.Entities;

/// <summary>Строка документа движения: товар и количество.</summary>
[Table("movement_items")]
public class MovementItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("movement_id")]
    public int MovementId { get; set; }

    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; }

    public Movement? Movement { get; set; }

    public Item? Item { get; set; }
}
