using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MomCare.Models;

[Table("addresses")]
public class Address
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("full_address")]
    public required string FullAddress { get; set; }

    [Column("ward")]
    public string? Ward { get; set; }

    [Column("district")]
    public string? District { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("is_default")]
    public bool IsDefault { get; set; }

    // Type: "customer_home", "nurse_base"
    [Column("type")]
    public string Type { get; set; } = "customer_home";

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;
}
