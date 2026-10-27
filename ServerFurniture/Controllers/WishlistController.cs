using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WishlistController : ControllerBase
{
    private readonly FurnitureContext _db;

    public WishlistController(FurnitureContext db)
    {
        _db = db;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    // POST /api/wishlist/products/{productId}/toggle
    [HttpPost("products/{productId:int}/toggle")]
    public async Task<IActionResult> ToggleWishlist(int productId)
    {
        if (productId <= 0)
            return BadRequest(new { message = "ProductId không hợp lệ" });

        var userId = GetUserId();

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
            return NotFound(new { message = "Không tìm thấy sản phẩm" });

        var existing = await _db.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

        if (existing != null)
        {
            _db.Wishlists.Remove(existing);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Đã bỏ thích sản phẩm",
                productId,
                isWishlisted = false
            });
        }

        _db.Wishlists.Add(new Wishlist
        {
            UserId = userId,
            ProductId = productId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Đã thích sản phẩm",
            productId,
            isWishlisted = true
        });
    }
}
