using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class Product
{
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? Description { get; set; }

    public double LengthCm { get; set; }

    public double WidthCm { get; set; }

    public double HeightCm { get; set; }

    public double WeightKg { get; set; }

    public string BaseMaterial { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
