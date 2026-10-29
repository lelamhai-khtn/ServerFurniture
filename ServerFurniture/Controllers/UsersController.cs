using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Claims;

namespace ServerFurniture.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly FurnitureContext _db;
    public UsersController(FurnitureContext db) => _db = db;

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException());

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    // PUT /api/users/avatar
    [Authorize]
    [HttpPut("avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.AvatarUrl))
            return BadRequest(new { message = "AvatarUrl không được để trống" });

        var userId = GetUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        user.Avatar = request.AvatarUrl;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật avatar thành công",
            userId = user.UserId,
            avatar = user.Avatar
        });
    }

    // PUT /api/users/avatar/upload
    [Authorize]
    [HttpPut("avatar/upload")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File không được để trống" });

        // Kiểm tra định dạng file
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var fileExtension = Path.GetExtension(file.FileName).ToLower();

        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest(new { message = "Chỉ hỗ trợ định dạng: jpg, jpeg, png, gif" });

        // Kiểm tra kích thước file (tối đa 5MB)
        const long maxFileSize = 5 * 1024 * 1024;
        if (file.Length > maxFileSize)
            return BadRequest(new { message = "Kích thước file tối đa 5MB" });

        try
        {
            var userId = GetUserId();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng" });

            // Tạo tên file unique
            var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{fileExtension}";
            var uploadPath = Path.Combine("wwwroot", "avatars");

            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            var filePath = Path.Combine(uploadPath, fileName);

            // Lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Cập nhật Avatar URL trong database
            var avatarUrl = $"/avatars/{fileName}";
            user.Avatar = avatarUrl;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Tải lên avatar thành công",
                userId = user.UserId,
                avatar = user.Avatar
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi tải lên file", error = ex.Message });
        }
    }

    // DELETE /api/users/avatar
    [Authorize]
    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteAvatar()
    {
        try
        {
            var userId = GetUserId();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng" });

            if (string.IsNullOrWhiteSpace(user.Avatar))
                return BadRequest(new { message = "Người dùng chưa có avatar để xóa" });

            // Xóa file khỏi thư mục
            if (user.Avatar.StartsWith("/avatars/"))
            {
                var fileName = user.Avatar.Replace("/avatars/", "");
                var filePath = Path.Combine("wwwroot", "avatars", fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Xóa avatar URL khỏi database
            user.Avatar = null;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Xóa avatar thành công",
                userId = user.UserId
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi xóa avatar", error = ex.Message });
        }
    }

    // GET /api/users/profile
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        return Ok(new
        {
            userId = user.UserId,
            email = user.Email,
            fullName = user.FullName,
            phone = user.Phone,
            gender = user.Gender,
            address = user.Address,
            avatar = user.Avatar,
            createdAt = user.CreatedAt
        });
    }

    // PUT /api/users/profile
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Yêu cầu không hợp lệ" });

        var userId = GetUserId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return NotFound(new { message = "Không tìm thấy người dùng" });

        // Cập nhật các trường được cho phép
        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName;

        if (!string.IsNullOrWhiteSpace(request.Phone))
            user.Phone = request.Phone;

        if (!string.IsNullOrWhiteSpace(request.Gender))
            user.Gender = request.Gender;

        if (!string.IsNullOrWhiteSpace(request.Address))
            user.Address = request.Address;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật thông tin thành công",
            userId = user.UserId,
            fullName = user.FullName,
            phone = user.Phone,
            gender = user.Gender,
            address = user.Address
        });
    }
}

public class UpdateAvatarRequest
{
    public string? AvatarUrl { get; set; }
}

public class UpdateProfileRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
}
