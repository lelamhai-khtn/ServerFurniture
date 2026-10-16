using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerFurniture.Models;

[Table("RefreshTokens")]
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool Revoked { get; set; }

    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ReplacedByToken { get; set; }

    public User? User { get; set; }
}
