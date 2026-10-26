using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class Product
{
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public int? StockQuantity { get; set; }

    public string? Color { get; set; }

    public string? UpholsteryMaterial { get; set; }

    public double LengthCm { get; set; }

    public double WidthCm { get; set; }

    public double HeightCm { get; set; }

    public double WeightKg { get; set; }

    public string BaseMaterial { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
