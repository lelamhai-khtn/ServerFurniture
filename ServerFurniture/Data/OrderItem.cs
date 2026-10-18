using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }

    public int VariantId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ProductVariant Variant { get; set; } = null!;
}
