using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Cryptography;
using ServerFurniture.Services;

namespace ServerFurniture.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly FurnitureContext _db;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(FurnitureContext db, ITokenService tokens, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public record LoginRequest(string Email, string Password);

    public record RegisterRequest(string FullName, string Email, string Password);

    public record UserDto(
        int UserId, 
        string Email, 
        string FullName, 
        string? Phone, 
        int RoleId,
        string? Gender,
        string? Address,
        string? Avatar,
        DateTime? CreatedAt);

    public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

    public record LoginResponse(
        string AccessToken, 
        string RefreshToken, 
        DateTime ExpiresAt,
        UserDto User);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest("FullName không được để trống");

        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest("Email không được để trống");

        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Password không được để trống");

        // Validate email format
        try
        {
            var addr = new System.Net.Mail.MailAddress(req.Email);
            if (addr.Address != req.Email)
                return BadRequest("Email không hợp lệ");
        }
        catch
        {
            return BadRequest("Email không hợp lệ");
        }

        // Normalize email (lowercase)
        var normalizedEmail = req.Email.ToLower().Trim();

        // Check if email already exists (with normalized check for race condition)
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (existingUser != null)
            return BadRequest("Email đã được đăng ký");

        // Hash password
        var hashedPassword = PasswordHasher.Hash(req.Password);

        // Create new user
        var newUser = new User
        {
            FullName = req.FullName,
            Email = normalizedEmail,
            PasswordHash = hashedPassword,
            CreatedAt = DateTime.UtcNow,
            RoleId = 2  // Default role for regular users
        };

        try
        {
            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Handle unique constraint violation (race condition)
            if (ex.InnerException?.Message.Contains("duplicate") ?? false)
                return BadRequest("Email đã được đăng ký");
            throw;
        }

        // Create tokens
        var access = _tokens.CreateAccessToken(newUser);
        var refresh = _tokens.CreateRefreshToken(newUser.UserId, TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "30")));

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        // Create response
        var userDto = new UserDto(
            newUser.UserId,
            newUser.Email,
            newUser.FullName,
            newUser.Phone,
            newUser.RoleId,
            newUser.Gender,
            newUser.Address,
            newUser.Avatar,
            newUser.CreatedAt);

        return Ok(new LoginResponse(access, refresh.Token, refresh.ExpiresAt, userDto));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return Unauthorized("Invalid credentials");

        // Check if user has password (registered with email/password)
        if (string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized("User was registered with Google. Please use Google Sign-In");

        // Verify password using PBKDF2 helper
        if (!PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken(user.UserId, TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "30")));

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var userDto = new UserDto(
            user.UserId,
            user.Email,
            user.FullName,
            user.Phone,
            user.RoleId,
            user.Gender,
            user.Address,
            user.Avatar,
            user.CreatedAt);

        return Ok(new LoginResponse(access, refresh.Token, refresh.ExpiresAt, userDto));
    }

    public record GoogleLoginRequest(string IdToken);

    [HttpPost("google")]
    public async Task<IActionResult> GoogleSignIn([FromBody] GoogleLoginRequest req)
    {
        // Verify token with Google
        var client = _httpClientFactory.CreateClient();
        var verifyUrl = $"https://oauth2.googleapis.com/tokeninfo?id_token={req.IdToken}";
        var res = await client.GetAsync(verifyUrl);
        if (!res.IsSuccessStatusCode) return Unauthorized("Invalid Google token");

        var payload = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var email = payload.RootElement.GetProperty("email").GetString();
        var sub = payload.RootElement.GetProperty("sub").GetString();
        var name = payload.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;
        var picture = payload.RootElement.TryGetProperty("picture", out var p) ? p.GetString() : null;

        if (email == null || sub == null) return Unauthorized("Invalid Google token payload");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == sub);

        if (user == null)
        {
            // Check if user exists by email but not linked to Google
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user != null)
            {
                // Link Google account to existing email
                user.GoogleId = sub;
                user.Avatar = picture ?? user.Avatar;
                _db.Users.Update(user);
            }
            else
            {
                // Create new user from Google
                user = new User
                {
                    Email = email,
                    GoogleId = sub,
                    FullName = name ?? email,
                    Avatar = picture,
                    CreatedAt = DateTime.UtcNow,
                    RoleId = 2  // Default role for regular users
                };
                _db.Users.Add(user);
            }
            await _db.SaveChangesAsync();
        }
        else
        {
            // User already linked, just update avatar if changed
            if (!string.IsNullOrEmpty(picture) && user.Avatar != picture)
            {
                user.Avatar = picture;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
            }
        }

        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken(user.UserId, TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "30")));
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        var userDto = new UserDto(
            user.UserId,
            user.Email,
            user.FullName,
            user.Phone,
            user.RoleId,
            user.Gender,
            user.Address,
            user.Avatar,
            user.CreatedAt);

        return Ok(new LoginResponse(access, refresh.Token, refresh.ExpiresAt, userDto));
    }

    public record RefreshRequest(string RefreshToken);

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var existing = await _db.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == req.RefreshToken);
        if (existing == null || existing.Revoked || existing.ExpiresAt < DateTime.UtcNow)
            return Unauthorized("Invalid refresh token");

        // revoke existing and issue new refresh token
        existing.Revoked = true;
        var newRefresh = _tokens.CreateRefreshToken(existing.UserId, TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "30")));
        newRefresh.ReplacedByToken = null;
        existing.ReplacedByToken = newRefresh.Token;

        _db.RefreshTokens.Add(newRefresh);
        await _db.SaveChangesAsync();

        var access = _tokens.CreateAccessToken(existing.User!);
        return Ok(new TokenResponse(access, newRefresh.Token, newRefresh.ExpiresAt));
    }
}

// Password hashing moved to Services/PasswordHasher
