using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class Order
{
    public int OrderId { get; set; }

    public int UserId { get; set; }

    public string? OrderStatus { get; set; }

    public decimal TotalAmount { get; set; }

    public DateOnly? DeliveryDate { get; set; }

    public string? DeliveryTimeSlot { get; set; }

    public int? ShipperInstallerId { get; set; }

    public string ShippingAddress { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Payment? Payment { get; set; }

    public virtual User? ShipperInstaller { get; set; }

    public virtual User User { get; set; } = null!;
}
