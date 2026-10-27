using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly FurnitureContext _db;

    public ProductsController(FurnitureContext db)
    {
        _db = db;
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return int.TryParse(value, out var userId) ? userId : null;
    }

    private async Task<HashSet<int>> GetWishlistedProductIdsAsync(IEnumerable<int> productIds)
    {
        var userId = GetUserId();
        var ids = productIds.Distinct().ToList();

        if (!userId.HasValue || ids.Count == 0)
            return new HashSet<int>();

        return await _db.Wishlists
            .AsNoTracking()
            .Where(w => w.UserId == userId.Value && ids.Contains(w.ProductId))
            .Select(w => w.ProductId)
            .ToHashSetAsync();
    }

    private static object MapProductResponse(Product p, string baseUrl, bool isWishlisted = false)
    {
        var discountPercent = (p.Price > 0 && p.DiscountPrice.HasValue && p.DiscountPrice < p.Price)
            ? Math.Round((1 - (p.DiscountPrice.Value / p.Price)) * 100, 2)
            : 0;
        var originalPrice = p.Price;
        var discountPrice = p.DiscountPrice ?? p.Price;

        var imageUrl = !string.IsNullOrEmpty(p.ImageUrl)
            ? $"{baseUrl}{p.ImageUrl}"
            : !string.IsNullOrEmpty(p.Category?.ImageUrl)
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
            p.Color,
            p.UpholsteryMaterial,
            stockQuantity = p.StockQuantity ?? 0,
            p.BaseMaterial,
            p.CreatedAt,
            isWishlisted,
            dimensions = new
            {
                p.LengthCm,
                p.WidthCm,
                p.HeightCm,
                p.WeightKg
            }
        };
    }

    /// <summary>
    /// Lấy top các sản phẩm có phần trăm giảm giá (discount) cao nhất (mặc định lấy 3 sản phẩm cho banner)
    /// Endpoint: GET /api/products/top-discounts?limit=3 hoặc GET /api/products/best-deals
    /// </summary>
    [HttpGet("top-discounts")]
    public async Task<IActionResult> GetTopDiscounts([FromQuery] int limit = 3)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        // Lấy danh sách sản phẩm có giảm giá, sắp xếp theo % giảm giá cao nhất
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.DiscountPrice != null && p.DiscountPrice < p.Price && p.Price > 0)
            .OrderByDescending(p => (p.Price - p.DiscountPrice!.Value) / p.Price)
            .Take(limit)
            .ToListAsync();

        // Fallback: nếu DB chưa có sản phẩm nào có discount > 0, lấy các sản phẩm mới nhất
        if (products.Count == 0)
        {
            products = await _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        var wishlistedProductIds = await GetWishlistedProductIdsAsync(products.Select(p => p.ProductId));
        var result = products.Select(p => MapProductResponse(p, baseUrl, wishlistedProductIds.Contains(p.ProductId)));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách các sản phẩm mới nhất (mặc định lấy 5 sản phẩm mới nhất thuộc bất kỳ category nào)
    /// Endpoint: GET /api/products/newest?limit=5
    /// </summary>
    [HttpGet("newest")]
    public async Task<IActionResult> GetNewArrivals([FromQuery] int limit = 5)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.ProductId)
            .Take(limit)
            .ToListAsync();

        var wishlistedProductIds = await GetWishlistedProductIdsAsync(products.Select(p => p.ProductId));
        var result = products.Select(p => MapProductResponse(p, baseUrl, wishlistedProductIds.Contains(p.ProductId)));
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
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var wishlistedProductIds = await GetWishlistedProductIdsAsync(products.Select(p => p.ProductId));
        var result = products.Select(p => MapProductResponse(p, baseUrl, wishlistedProductIds.Contains(p.ProductId)));
        return Ok(result);
    }

    /// <summary>
    /// Tìm kiếm sản phẩm theo tên, có thể lọc theo categoryId và khoảng giá.
    /// Nếu không truyền filter, trả về danh sách sản phẩm mới nhất.
    /// Endpoint: GET /api/products/search?name=sofa&categoryId=1&minPrice=100&maxPrice=500
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? name = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int limit = 20)
    {
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice)
            return BadRequest(new { message = "Khoảng giá không hợp lệ" });

        if (limit <= 0)
            limit = 20;

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(p => EF.Functions.Like(p.ProductName, $"%{name}%"));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= maxPrice.Value);
        }

        var products = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.ProductId)
            .Take(limit)
            .ToListAsync();

        var wishlistedProductIds = await GetWishlistedProductIdsAsync(products.Select(p => p.ProductId));
        var result = products.Select(p => MapProductResponse(p, baseUrl, wishlistedProductIds.Contains(p.ProductId)));
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
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null)
            return NotFound(new { message = "Không tìm thấy sản phẩm" });

        var isWishlistedSet = await GetWishlistedProductIdsAsync(new[] { product.ProductId });
        var result = MapProductResponse(product, baseUrl, isWishlistedSet.Contains(product.ProductId));
        return Ok(result);
    }
}
