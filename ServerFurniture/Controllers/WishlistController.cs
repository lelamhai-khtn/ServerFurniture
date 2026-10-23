using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

public class FavoriteRequest
{
    public int? UserId { get; set; }
    public int ProductId { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Route("api/favorites")]
public class WishlistController : ControllerBase
{
    private readonly FurnitureContext _db;

    public WishlistController(FurnitureContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Thêm hoặc Bỏ yêu thích một sản phẩm (Toggle Favorite)
    /// Khi người dùng nhấn nút hình trái tim: nếu chưa thích thì thêm vào, nếu đã thích rồi thì xóa bỏ.
    /// Endpoint: POST /api/wishlist/toggle hoặc POST /api/favorites/toggle
    /// Body JSON: { "userId": 1, "productId": 2 }
    /// </summary>
    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleFavorite([FromBody] FavoriteRequest request)
    {
        var targetUserId = GetCurrentUserId(request.UserId);
        if (!targetUserId.HasValue)
            return BadRequest(new { message = "Vui lòng cung cấp userId hoặc đăng nhập để tiếp tục." });

        // Kiểm tra sản phẩm có tồn tại hay không
        var productExists = await _db.Products.AnyAsync(p => p.ProductId == request.ProductId);
        if (!productExists)
            return NotFound(new { message = "Không tìm thấy sản phẩm." });

        // Kiểm tra xem sản phẩm đã có trong wishlist chưa
        var existing = await _db.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == targetUserId.Value && w.ProductId == request.ProductId);

        if (existing != null)
        {
            // Đã có -> Xóa bỏ (Unfavorite)
            _db.Wishlists.Remove(existing);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                isFavorited = false,
                message = "Đã xóa sản phẩm khỏi danh sách yêu thích.",
                productId = request.ProductId,
                userId = targetUserId.Value
            });
        }
        else
        {
            // Chưa có -> Thêm mới vào (Favorite)
            var wishlistItem = new Wishlist
            {
                UserId = targetUserId.Value,
                ProductId = request.ProductId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Wishlists.Add(wishlistItem);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                isFavorited = true,
                message = "Đã thêm sản phẩm vào danh sách yêu thích thành công.",
                wishlistId = wishlistItem.WishlistId,
                productId = request.ProductId,
                userId = targetUserId.Value
            });
        }
    }

    /// <summary>
    /// Thêm sản phẩm vào yêu thích
    /// Endpoint: POST /api/wishlist hoặc POST /api/favorites
    /// Body JSON: { "userId": 1, "productId": 2 }
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddFavorite([FromBody] FavoriteRequest request)
    {
        var targetUserId = GetCurrentUserId(request.UserId);
        if (!targetUserId.HasValue)
            return BadRequest(new { message = "Vui lòng cung cấp userId hoặc đăng nhập để tiếp tục." });

        var productExists = await _db.Products.AnyAsync(p => p.ProductId == request.ProductId);
        if (!productExists)
            return NotFound(new { message = "Không tìm thấy sản phẩm." });

        var exists = await _db.Wishlists
            .AnyAsync(w => w.UserId == targetUserId.Value && w.ProductId == request.ProductId);

        if (exists)
        {
            return Ok(new
            {
                isFavorited = true,
                message = "Sản phẩm đã có trong danh sách yêu thích rồi."
            });
        }

        var wishlistItem = new Wishlist
        {
            UserId = targetUserId.Value,
            ProductId = request.ProductId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Wishlists.Add(wishlistItem);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            isFavorited = true,
            message = "Đã thêm vào danh sách yêu thích thành công.",
            wishlistId = wishlistItem.WishlistId
        });
    }

    /// <summary>
    /// Xóa sản phẩm khỏi danh sách yêu thích
    /// Endpoint: DELETE /api/wishlist/{productId}?userId=1 hoặc DELETE /api/favorites/{productId}?userId=1
    /// </summary>
    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> RemoveFavorite(int productId, [FromQuery] int? userId = null)
    {
        var targetUserId = GetCurrentUserId(userId);
        if (!targetUserId.HasValue)
            return BadRequest(new { message = "Vui lòng cung cấp userId hoặc đăng nhập để tiếp tục." });

        var item = await _db.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == targetUserId.Value && w.ProductId == productId);

        if (item == null)
            return NotFound(new { message = "Sản phẩm không có trong danh sách yêu thích." });

        _db.Wishlists.Remove(item);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            isFavorited = false,
            message = "Đã xóa sản phẩm khỏi danh sách yêu thích."
        });
    }

    /// <summary>
    /// Lấy danh sách tất cả các sản phẩm yêu thích của 1 người dùng
    /// Endpoint: GET /api/wishlist?userId=1 hoặc GET /api/favorites?userId=1
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserWishlist([FromQuery] int? userId = null)
    {
        var targetUserId = GetCurrentUserId(userId);
        if (!targetUserId.HasValue)
            return BadRequest(new { message = "Vui lòng cung cấp userId hoặc đăng nhập để tiếp tục." });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var wishlists = await _db.Wishlists
            .AsNoTracking()
            .Where(w => w.UserId == targetUserId.Value)
            .Include(w => w.Product)
                .ThenInclude(p => p.Category)
            .Include(w => w.Product)
                .ThenInclude(p => p.ProductVariants)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        var result = wishlists.Select(w =>
        {
            var p = w.Product;
            var defaultVariant = p.ProductVariants
                .OrderByDescending(v => v.DiscountPercent ?? 0)
                .FirstOrDefault();

            var discountPercent = defaultVariant?.DiscountPercent ?? 0;
            var originalPrice = defaultVariant?.Price ?? 0;
            var discountPrice = defaultVariant?.DiscountPrice 
                ?? (discountPercent > 0 ? originalPrice * (1 - (discountPercent / 100)) : originalPrice);

            var imageUrl = !string.IsNullOrEmpty(p.Category?.ImageUrl)
                ? $"{baseUrl}{p.Category.ImageUrl}"
                : $"{baseUrl}/images/chair.png";

            return new
            {
                wishlistId = w.WishlistId,
                favoritedAt = w.CreatedAt,
                product = new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Description,
                    p.CategoryId,
                    categoryName = p.Category?.CategoryName,
                    imageUrl,
                    discountPercent,
                    originalPrice,
                    discountPrice,
                    p.BaseMaterial
                }
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Kiểm tra xem 1 sản phẩm cụ thể đã được người dùng yêu thích chưa (để hiển thị icon trái tim rỗng hay đỏ)
    /// Endpoint: GET /api/wishlist/check?userId=1&productId=2
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> CheckFavorite([FromQuery] int productId, [FromQuery] int? userId = null)
    {
        var targetUserId = GetCurrentUserId(userId);
        if (!targetUserId.HasValue)
            return Ok(new { isFavorited = false });

        var isFavorited = await _db.Wishlists
            .AnyAsync(w => w.UserId == targetUserId.Value && w.ProductId == productId);

        return Ok(new { productId, isFavorited });
    }

    private int? GetCurrentUserId(int? requestUserId)
    {
        if (requestUserId.HasValue && requestUserId.Value > 0)
            return requestUserId.Value;

        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (int.TryParse(subClaim, out var id))
            return id;

        return null;
    }
}
