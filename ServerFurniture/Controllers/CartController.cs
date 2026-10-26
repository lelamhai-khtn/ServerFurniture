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

    public record AddCartItemRequest(int ProductId, int Quantity = 1);
    public record UpdateCartItemRequest(int Quantity);

    // GET /api/cart
    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var userId = GetUserId();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var cartItems = await _db.Carts
            .AsNoTracking()
            .Include(c => c.Product)
                .ThenInclude(p => p.Category)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CartId)
            .ToListAsync();

        var items = cartItems.Select(ci =>
        {
            var p = ci.Product;
            var imageUrl = !string.IsNullOrEmpty(p?.ImageUrl)
                ? $"{baseUrl}{p.ImageUrl}"
                : !string.IsNullOrEmpty(p?.Category?.ImageUrl)
                    ? $"{baseUrl}{p.Category.ImageUrl}"
                    : $"{baseUrl}/images/chair.png";

            var effectivePrice = p?.DiscountPrice ?? p?.Price ?? 0;

            return new
            {
                cartId = ci.CartId,
                productId = ci.ProductId,
                ci.Quantity,
                unitPrice = effectivePrice,
                subTotal = effectivePrice * ci.Quantity,
                product = p == null ? null : (object)new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Description,
                    p.Price,
                    p.DiscountPrice,
                    p.Color,
                    p.UpholsteryMaterial,
                    stockQuantity = p.StockQuantity ?? 0,
                    imageUrl
                }
            };
        }).ToList();

        var latestUpdatedAt = cartItems.Max(i => i.UpdatedAt);

        return Ok(new
        {
            userId,
            updatedAt = latestUpdatedAt,
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

        if (req.ProductId <= 0)
            return BadRequest(new { message = "ProductId không hợp lệ" });

        var userId = GetUserId();

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == req.ProductId);

        if (product == null)
            return NotFound(new { message = "Không tìm thấy sản phẩm" });

        if (product.StockQuantity.HasValue && product.StockQuantity.Value < req.Quantity)
            return BadRequest(new { message = $"Số lượng trong kho không đủ (còn {product.StockQuantity} sản phẩm)" });

        var existingItem = await _db.Carts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == req.ProductId);

        if (existingItem != null)
        {
            var newQty = existingItem.Quantity + req.Quantity;
            if (product.StockQuantity.HasValue && newQty > product.StockQuantity.Value)
                return BadRequest(new { message = $"Số lượng vượt quá tồn kho (còn {product.StockQuantity} sản phẩm)" });
            existingItem.Quantity = newQty;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Carts.Add(new Cart
            {
                UserId = userId,
                ProductId = req.ProductId,
                Quantity = req.Quantity,
                UpdatedAt = DateTime.UtcNow
            });
        }

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
        var cartItem = await _db.Carts
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.CartId == cartItemId);

        if (cartItem == null) return NotFound(new { message = "Không tìm thấy item trong giỏ hàng" });
        if (cartItem.UserId != userId) return Forbid();

        var stock = cartItem.Product?.StockQuantity;
        if (stock.HasValue && req.Quantity > stock.Value)
            return BadRequest(new { message = $"Số lượng vượt quá tồn kho (còn {stock} sản phẩm)" });

        cartItem.Quantity = req.Quantity;
        cartItem.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã cập nhật số lượng", cartItemId, quantity = req.Quantity });
    }

    // DELETE /api/cart/items/{cartItemId}
    [HttpDelete("items/{cartItemId:int}")]
    public async Task<IActionResult> RemoveItem(int cartItemId)
    {
        var userId = GetUserId();
        var cartItem = await _db.Carts
            .FirstOrDefaultAsync(c => c.CartId == cartItemId);

        if (cartItem == null) return NotFound(new { message = "Không tìm thấy item trong giỏ hàng" });
        if (cartItem.UserId != userId) return Forbid();

        _db.Carts.Remove(cartItem);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã xóa sản phẩm khỏi giỏ hàng" });
    }

    // DELETE /api/cart
    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = GetUserId();
        var userCartItems = await _db.Carts
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (userCartItems.Count == 0)
            return Ok(new { message = "Giỏ hàng đã trống" });

        _db.Carts.RemoveRange(userCartItems);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Đã xóa toàn bộ giỏ hàng" });
    }
}
