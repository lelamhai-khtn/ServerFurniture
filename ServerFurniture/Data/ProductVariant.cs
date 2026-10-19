using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class ProductVariant
{
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    public string Color { get; set; } = null!;

    public string? UpholsteryMaterial { get; set; }

    public decimal Price { get; set; }

    public int? StockQuantity { get; set; }

    public decimal? DiscountPrice { get; set; }

    public decimal? DiscountPercent { get; set; }

    public bool? IsOnSale { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;
}
