using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ServerFurniture.Data;
using System.Security.Cryptography;

namespace ServerFurniture.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string CreateAccessToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("fullname", user.FullName ?? string.Empty)
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "60"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken CreateRefreshToken(int userId, TimeSpan lifetime)
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        var random = Convert.ToBase64String(bytes);
        return new RefreshToken
        {
            Token = random,
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Revoked = false
        };
    }
}
