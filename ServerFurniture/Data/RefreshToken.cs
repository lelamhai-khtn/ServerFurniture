using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class RefreshToken
{
    public int Id { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool Revoked { get; set; }

    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    public virtual User User { get; set; } = null!;
}
