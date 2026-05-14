using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("services")]
public class Service
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("category")]
    public string Category { get; set; } = "cham-soc-sau-sinh";

    [Column("base_price")]
    public decimal BasePrice { get; set; }

    [Column("estimated_duration_minutes")]
    public int EstimatedDurationMinutes { get; set; }

    // Status: "active", "inactive"
    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<NurseService> NurseServices { get; set; } = new List<NurseService>();
}
