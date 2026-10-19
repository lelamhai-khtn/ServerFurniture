using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;

namespace ServerFurniture.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly FurnitureContext _db;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public CategoriesController(FurnitureContext db, IWebHostEnvironment webHostEnvironment)
    {
        _db = db;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _db.Categories.AsNoTracking().ToListAsync();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var result = categories.Select(c => new
        {
            c.CategoryId,
            c.ParentId,
            c.CategoryName,
            imageUrl = string.IsNullOrEmpty(c.ImageUrl) ? null : $"{baseUrl}{c.ImageUrl}"
        });

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category == null) return NotFound();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var result = new
        {
            category.CategoryId,
            category.ParentId,
            category.CategoryName,
            imageUrl = string.IsNullOrEmpty(category.ImageUrl) ? null : $"{baseUrl}{category.ImageUrl}"
        };

        return Ok(result);
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn một hình ảnh");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var fileExtension = Path.GetExtension(file.FileName).ToLower();

        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest("Chỉ hỗ trợ các định dạng: jpg, jpeg, png, gif");

        try
        {
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString() + fileExtension;
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"/images/{fileName}";
            return Ok(new { imageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi upload hình ảnh", error = ex.Message });
        }
    }
}
