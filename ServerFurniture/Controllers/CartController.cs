using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly FurnitureContext _db;

    public CartController(FurnitureContext db)
    {
        _db = db;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    private async Task<Cart> GetOrCreateCartAsync(int userId)
    {
        var cart = await _db.Carts
            .Include(c => c.CartItems)
                .ThenInclude(ci => ci.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId, UpdatedAt = DateTime.UtcNow };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync();
        }

        return cart;
    }

    public record AddCartItemRequest(int VariantId, int Quantity = 1);
    public record UpdateCartItemRequest(int Quantity);

    // GET /api/cart
    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var userId = GetUserId();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var cart = await GetOrCreateCartAsync(userId);

        var items = cart.CartItems.Select(ci =>
        {
            var v = ci.Variant;
            var p = v?.Product;
            var imageUrl = !string.IsNullOrEmpty(p?.Category?.ImageUrl)
                ? $"{baseUrl}{p.Category.ImageUrl}"
                : $"{baseUrl}/images/chair.png";
            var effectivePrice = (v?.IsOnSale == true && v.DiscountPrice.HasValue)
                ? v.DiscountPrice.Value : v?.Price ?? 0;

            return new
            {
                ci.CartItemId,
                ci.CartId,
                ci.VariantId,
                ci.Quantity,
                unitPrice = effectivePrice,
                subTotal = effectivePrice * ci.Quantity,
                variant = v == null ? null : (object)new
                {
                    v.VariantId,
                    v.Color,
                    v.UpholsteryMaterial,
                    v.Price,
                    v.DiscountPrice,
                    v.DiscountPercent,
                    v.IsOnSale,
                    v.StockQuantity
                },
                product = p == null ? null : (object)new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Description,
                    imageUrl
                }
            };
        }).ToList();

        return Ok(new
        {
            cart.CartId,
            cart.UserId,
            cart.UpdatedAt,
            totalItems = items.Sum(i => i.Quantity),
            totalAmount = items.Sum(i => i.subTotal),
            items
        });
    }

    // POST /api/cart/items
    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest req)
    {
        if (req.Quantity <= 0)
            return BadRequest(new { message = "Số lượng phải lớn hơn 0" });

        var userId = GetUserId();

        var variant = await _db.ProductVariants.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VariantId == req.VariantId);

        if (variant == null)
            return NotFound(new { message = "Không tìm thấy variant sản phẩm" });

        if (variant.StockQuantity.HasValue && variant.StockQuantity.Value < req.Quantity)
            return BadRequest(new { message = $"Số lượng trong kho không đủ (còn {variant.StockQuantity} sản phẩm)" });

        var cart = await GetOrCreateCartAsync(userId);

        var existingItem = await _db.CartItems
            .FirstOrDefaultAsync(ci => ci.CartId == cart.CartId && ci.VariantId == req.VariantId);

        if (existingItem != null)
        {
            var newQty = existingItem.Quantity + req.Quantity;
            if (variant.StockQuantity.HasValue && newQty > variant.StockQuantity.Value)
                return BadRequest(new { message = $"Số lượng vượt quá tồn kho (còn {variant.StockQuantity} sản phẩm)" });
            existingItem.Quantity = newQty;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                CartId = cart.CartId,
                VariantId = req.VariantId,
                Quantity = req.Quantity
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã thêm vào giỏ hàng thành công" });
    }

    // PUT /api/cart/items/{cartItemId}
    [HttpPut("items/{cartItemId:int}")]
    public async Task<IActionResult> UpdateItem(int cartItemId, [FromBody] UpdateCartItemRequest req)
    {
        if (req.Quantity <= 0)
            return BadRequest(new { message = "Số lượng phải lớn hơn 0" });

        var userId = GetUserId();
        var cartItem = await _db.CartItems
            .Include(ci => ci.Cart).Include(ci => ci.Variant)
            .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId);

        if (cartItem == null) return NotFound(new { message = "Không tìm thấy item trong giỏ hàng" });
        if (cartItem.Cart.UserId != userId) return Forbid();

        var stock = cartItem.Variant?.StockQuantity;
        if (stock.HasValue && req.Quantity > stock.Value)
            return BadRequest(new { message = $"Số lượng vượt quá tồn kho (còn {stock} sản phẩm)" });

        cartItem.Quantity = req.Quantity;
        cartItem.Cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã cập nhật số lượng", cartItemId, quantity = req.Quantity });
    }

    // DELETE /api/cart/items/{cartItemId}
    [HttpDelete("items/{cartItemId:int}")]
    public async Task<IActionResult> RemoveItem(int cartItemId)
    {
        var userId = GetUserId();
        var cartItem = await _db.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(ci => ci.CartItemId == cartItemId);

        if (cartItem == null) return NotFound(new { message = "Không tìm thấy item trong giỏ hàng" });
        if (cartItem.Cart.UserId != userId) return Forbid();

        _db.CartItems.Remove(cartItem);
        cartItem.Cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã xóa sản phẩm khỏi giỏ hàng" });
    }

    // DELETE /api/cart
    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();
        var cart = await _db.Carts.Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.CartItems.Any())
            return Ok(new { message = "Giỏ hàng đã trống" });

        _db.CartItems.RemoveRange(cart.CartItems);
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã xóa toàn bộ giỏ hàng" });
    }
}
