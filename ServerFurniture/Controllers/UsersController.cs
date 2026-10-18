using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;

namespace ServerFurniture.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly FurnitureContext _db;
    public UsersController(FurnitureContext db) => _db = db;

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
}
