using System.Security.Cryptography;

namespace Banccoon.Core.Security;

// The app lock PIN is a privacy speedbump against casual snooping, not encryption - the SQLite
// database it's gating stays unencrypted on disk regardless (see docs/development-phases.md's
// Phase 7 "optional encryption... always disabled by default" backlog note). Still, the PIN
// itself is salted and hashed rather than stored in plain text, since that costs nothing extra.
public static class PinHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 100_000;

    public static string GenerateSalt()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltSizeBytes));
    }

    public static string Hash(string pin, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(pin, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return Convert.ToBase64String(hashBytes);
    }

    public static bool Verify(string pin, string salt, string expectedHash)
    {
        var actualHash = Hash(pin, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(actualHash),
            Convert.FromBase64String(expectedHash));
    }
}
