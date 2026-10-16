using System.Security.Cryptography;

namespace ServerFurniture.Services;

public static class PasswordHasher
{
    // Stored format: {iterations}.{saltBase64}.{hashBase64}
    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        var parts = storedHash.Split('.');
        if (parts.Length != 3) return false;
        var iterations = int.Parse(parts[0]);
        var salt = Convert.FromBase64String(parts[1]);
        var hash = Convert.FromBase64String(parts[2]);

        using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var test = derive.GetBytes(hash.Length);
        return test.SequenceEqual(hash);
    }

    public static string Hash(string password, int iterations = 100_000)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        using var derive = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = derive.GetBytes(32);
        return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}
