using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerFurniture.Models;

[Table("Users")]
public class User
{
    [Key]
    public int UserId { get; set; }

    public int RoleId { get; set; }

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = null!;

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    [MaxLength(255)]
    public string? GoogleId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? FcmToken { get; set; }

    public DateTime? CreatedAt { get; set; }
}
