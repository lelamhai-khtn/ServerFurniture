using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServerFurniture.Data;
using System.Security.Cryptography;
using ServerFurniture.Models;
using ServerFurniture.Services;

namespace ServerFurniture.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(AppDbContext db, ITokenService tokens, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _tokens = tokens;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public record LoginRequest(string Email, string Password);
    public record TokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user == null) return Unauthorized("Invalid credentials");

        // Verify password using PBKDF2 helper
        if (!PasswordHasher.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken(user.UserId, TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "30")));

        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        return Ok(new TokenResponse(access, refresh.Token, refresh.ExpiresAt));
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

        if (email == null || sub == null) return Unauthorized("Invalid Google token payload");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == sub || u.Email == email);
        if (user == null)
        {
            user = new User
            {
                Email = email,
                GoogleId = sub,
                FullName = name ?? email,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken(user.UserId, TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpirationDays"] ?? "30")));
        _db.RefreshTokens.Add(refresh);
        await _db.SaveChangesAsync();

        return Ok(new TokenResponse(access, refresh.Token, refresh.ExpiresAt));
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
