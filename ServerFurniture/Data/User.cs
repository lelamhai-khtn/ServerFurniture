using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class User
{
    public int UserId { get; set; }

    public int RoleId { get; set; }

    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? GoogleId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? FcmToken { get; set; }

    public string? Gender { get; set; }

    public string? Address { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Avatar { get; set; }

    public virtual Cart? Cart { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> OrderShipperInstallers { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderUsers { get; set; } = new List<Order>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
