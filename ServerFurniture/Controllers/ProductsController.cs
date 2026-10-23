using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;

namespace ServerFurniture.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly FurnitureContext _db;

    public ProductsController(FurnitureContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lấy top các sản phẩm có phần trăm giảm giá (discount) cao nhất (mặc định lấy 3 sản phẩm cho banner)
    /// Endpoint: GET /api/products/top-discounts?limit=3 hoặc GET /api/products/best-deals
    /// </summary>
    [HttpGet("top-discounts")]
    [HttpGet("best-deals")]
    public async Task<IActionResult> GetTopDiscounts([FromQuery] int limit = 3)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        // Lấy danh sách sản phẩm có biến thể giảm giá, sắp xếp theo % giảm giá cao nhất
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductVariants)
            .Where(p => p.ProductVariants.Any(v => (v.DiscountPercent ?? 0) > 0))
            .OrderByDescending(p => p.ProductVariants.Max(v => v.DiscountPercent ?? 0))
            .Take(limit)
            .ToListAsync();

        // Fallback: nếu DB chưa có sản phẩm nào có discount > 0, lấy các sản phẩm mới nhất
        if (products.Count == 0)
        {
            products = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.ProductVariants)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        var result = products.Select(p =>
        {
            // Lấy biến thể có % giảm giá cao nhất của sản phẩm đó
            var bestVariant = p.ProductVariants
                .OrderByDescending(v => v.DiscountPercent ?? 0)
                .ThenBy(v => v.DiscountPrice ?? v.Price)
                .FirstOrDefault();

            var discountPercent = bestVariant?.DiscountPercent ?? 0;
            var originalPrice = bestVariant?.Price ?? 0;
            var discountPrice = bestVariant?.DiscountPrice 
                ?? (discountPercent > 0 ? originalPrice * (1 - (discountPercent / 100)) : originalPrice);

            var imageUrl = !string.IsNullOrEmpty(p.Category?.ImageUrl)
                ? $"{baseUrl}{p.Category.ImageUrl}"
                : $"{baseUrl}/images/chair.png";

            return new
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
                p.BaseMaterial,
                dimensions = new
                {
                    p.LengthCm,
                    p.WidthCm,
                    p.HeightCm,
                    p.WeightKg
                },
                variants = p.ProductVariants.Select(v => new
                {
                    v.VariantId,
                    v.Color,
                    v.UpholsteryMaterial,
                    v.Price,
                    v.DiscountPrice,
                    v.DiscountPercent,
                    v.IsOnSale,
                    v.StockQuantity
                })
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách các sản phẩm mới nhất (mặc định lấy 5 sản phẩm mới nhất thuộc bất kỳ category nào)
    /// Endpoint: GET /api/products/new-arrivals hoặc GET /api/products/latest
    /// </summary>
    [HttpGet("newest")]
    public async Task<IActionResult> GetNewArrivals([FromQuery] int limit = 5)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductVariants)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.ProductId)
            .Take(limit)
            .ToListAsync();

        var result = products.Select(p =>
        {
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
                p.ProductId,
                p.ProductName,
                p.Description,
                p.CategoryId,
                categoryName = p.Category?.CategoryName,
                imageUrl,
                discountPercent,
                originalPrice,
                discountPrice,
                p.BaseMaterial,
                p.CreatedAt,
                dimensions = new
                {
                    p.LengthCm,
                    p.WidthCm,
                    p.HeightCm,
                    p.WeightKg
                },
                variants = p.ProductVariants.Select(v => new
                {
                    v.VariantId,
                    v.Color,
                    v.UpholsteryMaterial,
                    v.Price,
                    v.DiscountPrice,
                    v.DiscountPercent,
                    v.IsOnSale,
                    v.StockQuantity
                })
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách sản phẩm (mặc định giới hạn 5 sản phẩm, có thể lọc theo categoryId)
    /// Endpoint: GET /api/products?categoryId=1 hoặc GET /api/products?categoryId=1&limit=5
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? categoryId = null, [FromQuery] int limit = 5)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductVariants)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var result = products.Select(p =>
        {
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
                p.ProductId,
                p.ProductName,
                p.Description,
                p.CategoryId,
                categoryName = p.Category?.CategoryName,
                imageUrl,
                discountPercent,
                originalPrice,
                discountPrice,
                p.BaseMaterial,
                variantsCount = p.ProductVariants.Count
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết một sản phẩm theo ID
    /// Endpoint: GET /api/products/{id}
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ProductVariants)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
            return NotFound(new { message = "Không tìm thấy sản phẩm" });

        var defaultVariant = product.ProductVariants
            .OrderByDescending(v => v.DiscountPercent ?? 0)
            .FirstOrDefault();

        var discountPercent = defaultVariant?.DiscountPercent ?? 0;
        var originalPrice = defaultVariant?.Price ?? 0;
        var discountPrice = defaultVariant?.DiscountPrice 
            ?? (discountPercent > 0 ? originalPrice * (1 - (discountPercent / 100)) : originalPrice);

        var imageUrl = !string.IsNullOrEmpty(product.Category?.ImageUrl)
            ? $"{baseUrl}{product.Category.ImageUrl}"
            : $"{baseUrl}/images/chair.png";

        var result = new
        {
            product.ProductId,
            product.ProductName,
            product.Description,
            product.CategoryId,
            categoryName = product.Category?.CategoryName,
            imageUrl,
            discountPercent,
            originalPrice,
            discountPrice,
            product.BaseMaterial,
            dimensions = new
            {
                product.LengthCm,
                product.WidthCm,
                product.HeightCm,
                product.WeightKg
            },
            variants = product.ProductVariants.Select(v => new
            {
                v.VariantId,
                v.Color,
                v.UpholsteryMaterial,
                v.Price,
                v.DiscountPrice,
                v.DiscountPercent,
                v.IsOnSale,
                v.StockQuantity
            })
        };

        return Ok(result);
    }

    /// <summary>
    /// Thêm hoặc bỏ yêu thích sản phẩm (Toggle Favorite)
    /// Endpoint: POST /api/products/{id}/favorite?userId=1
    /// </summary>
    [HttpPost("{id:int}/favorite")]
    public async Task<IActionResult> ToggleFavorite(int id, [FromQuery] int? userId = null, [FromBody] FavoriteRequest? request = null)
    {
        var targetUserId = userId ?? request?.UserId;
        if (!targetUserId.HasValue)
        {
            var subClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            if (int.TryParse(subClaim, out var parsedId))
                targetUserId = parsedId;
        }

        if (!targetUserId.HasValue)
            return BadRequest(new { message = "Vui lòng cung cấp userId hoặc đăng nhập để tiếp tục." });

        var productExists = await _db.Products.AnyAsync(p => p.ProductId == id);
        if (!productExists)
            return NotFound(new { message = "Không tìm thấy sản phẩm." });

        var existing = await _db.Wishlists
            .FirstOrDefaultAsync(w => w.UserId == targetUserId.Value && w.ProductId == id);

        if (existing != null)
        {
            _db.Wishlists.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { isFavorited = false, message = "Đã xóa khỏi danh sách yêu thích.", productId = id });
        }
        else
        {
            var wishlistItem = new Wishlist
            {
                UserId = targetUserId.Value,
                ProductId = id,
                CreatedAt = DateTime.UtcNow
            };
            _db.Wishlists.Add(wishlistItem);
            await _db.SaveChangesAsync();
            return Ok(new { isFavorited = true, message = "Đã thêm vào danh sách yêu thích.", productId = id });
        }
    }
}
