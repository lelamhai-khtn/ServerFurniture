using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string PaymentGateway { get; set; } = null!;

    public string? TransactionId { get; set; }

    public string? PaymentStatus { get; set; }

    public string? WebhookRawData { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
