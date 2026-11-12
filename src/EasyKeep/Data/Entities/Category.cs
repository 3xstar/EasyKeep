using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyKeep.Data.Entities;

/// <summary>Категория товаров.</summary>
[Table("categories")]
public class Category
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    public List<Item> Items { get; set; } = new();
}
