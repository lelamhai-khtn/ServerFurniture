using System;
using System.Collections.Generic;

namespace ServerFurniture.Data;

public partial class Category
{
    public int CategoryId { get; set; }

    public int? ParentId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
