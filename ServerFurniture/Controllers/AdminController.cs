using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using ServerFurniture.Services;

namespace ServerFurniture.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly FurnitureContext _db;
    private readonly IWebHostEnvironment _env;

    public AdminController(FurnitureContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Reset all users' password to a known value (only allowed in Development environment)
    [HttpPost("reset-all-passwords")]
    public async Task<IActionResult> ResetAllPasswords([FromQuery] string? newPassword)
    {
        if (!_env.IsDevelopment())
            return Forbid("This operation is allowed only in Development environment.");

        var pwd = newPassword ?? "123456";
        var hashed = PasswordHasher.Hash(pwd);

        var users = await _db.Users.ToListAsync();
        foreach (var u in users)
        {
            u.PasswordHash = hashed;
        }

        await _db.SaveChangesAsync();
        return Ok(new { Count = users.Count, Password = pwd });
    }
}
