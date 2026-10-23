using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ServerFurniture.Data;

public partial class FurnitureContext : DbContext
{
    public FurnitureContext()
    {
    }

    public FurnitureContext(DbContextOptions<FurnitureContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderItem> OrderItems { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductVariant> ProductVariants { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Wishlist> Wishlists { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Carts__51BCD7B7AF637FE4");

            entity.HasIndex(e => e.UserId, "UQ__Carts__1788CC4DCB26C7F9").IsUnique();

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.User).WithOne(p => p.Cart)
                .HasForeignKey<Cart>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Carts__UserId__4CA06362");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.CartItemId).HasName("PK__CartItem__488B0B0A12457792");

            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("FK__CartItems__CartI__5070F446");

            entity.HasOne(d => d.Variant).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CartItems__Varia__5165187F");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0B47EF3484");

            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12BB540FE6");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__UserI__6A30C649");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BCFB0AC93B5");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DeliveryTimeSlot).HasMaxLength(50);
            entity.Property(e => e.OrderStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Chờ xác nhận");
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ShipperInstaller).WithMany(p => p.OrderShipperInstallers)
                .HasForeignKey(d => d.ShipperInstallerId)
                .HasConstraintName("FK__Orders__ShipperI__571DF1D5");

            entity.HasOne(d => d.User).WithMany(p => p.OrderUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Orders__UserId__5629CD9C");
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId).HasName("PK__OrderIte__57ED0681BDF783E3");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderItem__Order__59FA5E80");

            entity.HasOne(d => d.Variant).WithMany(p => p.OrderItems)
                .HasForeignKey(d => d.VariantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderItem__Varia__5AEE82B9");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payments__9B556A3868A86E5F");

            entity.HasIndex(e => e.OrderId, "UQ__Payments__C3905BCEB2635AFF").IsUnique();

            entity.Property(e => e.PaidAt).HasColumnType("datetime");
            entity.Property(e => e.PaymentGateway).HasMaxLength(50);
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Chờ thanh toán");
            entity.Property(e => e.TransactionId)
                .HasMaxLength(255)
                .IsUnicode(false);

            entity.HasOne(d => d.Order).WithOne(p => p.Payment)
                .HasForeignKey<Payment>(d => d.OrderId)
                .HasConstraintName("FK__Payments__OrderI__5FB337D6");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6CD16C48659");

            entity.Property(e => e.BaseMaterial).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.HeightCm).HasColumnName("Height_cm");
            entity.Property(e => e.LengthCm).HasColumnName("Length_cm");
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.WeightKg).HasColumnName("Weight_kg");
            entity.Property(e => e.WidthCm).HasColumnName("Width_cm");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Products__Catego__4316F928");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(e => e.VariantId).HasName("PK__ProductV__0EA23384A7B54033");

            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.DiscountPercent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.DiscountPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsOnSale).HasDefaultValue(false);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.StockQuantity).HasDefaultValue(0);
            entity.Property(e => e.UpholsteryMaterial).HasMaxLength(100);

            entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ProductVa__Produ__47DBAE45");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__RefreshT__3214EC0770EF6435");

            entity.HasIndex(e => e.UserId, "IX_RefreshTokens_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1AB752BE28");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160B313E24B").IsUnique();

            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4CD00CD421");

            entity.HasIndex(e => e.GoogleId, "UQ_Users_GoogleId")
                .IsUnique()
                .HasFilter("([GoogleId] IS NOT NULL)");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534762F886B").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Avatar).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FcmToken).HasMaxLength(500);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.GoogleId).HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__3C69FB99");
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.HasKey(e => e.WishlistId).HasName("PK__Wishlist__233189EBA8125CE5");

            entity.HasIndex(e => new { e.UserId, e.ProductId }, "UQ__Wishlist__DCC80021A4833C39").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Product).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__Wishlists__Produ__656C112C");

            entity.HasOne(d => d.User).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Wishlists__UserI__6477ECF3");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
