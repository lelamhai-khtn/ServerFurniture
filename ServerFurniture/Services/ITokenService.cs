using ServerFurniture.Data;

namespace ServerFurniture.Services;

public interface ITokenService
{
    string CreateAccessToken(User user);
    RefreshToken CreateRefreshToken(int userId, TimeSpan lifetime);
}
