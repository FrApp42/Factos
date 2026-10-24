using System.Security.Cryptography;
using System.Text;

namespace Factos.Data;

/// Laravel Sanctum 4 API token. The plaintext is shown once at creation; only its
/// SHA-256 hex digest is stored, and clients send "{Id}|{plaintext}" as a Bearer token.
public class PersonalAccessToken
{
    public int Id { get; set; }
    public int TokenableId { get; set; }
    /// Morph alias from InvoiceShelf's ModelIdentityMap.
    public string TokenableType { get; set; } = "user";
    public string Name { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string? Abilities { get; set; } = "[\"*\"]";
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// Sanctum's generateTokenString(): Str::random(40) followed by the crc32b of
    /// that entropy as 8 hex digits (token_prefix is unset upstream).
    public static string NewPlaintext()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var entropy = RandomNumberGenerator.GetString(alphabet, 40);
        return entropy + Crc32b(Encoding.ASCII.GetBytes(entropy)).ToString("x8");
    }

    public static string Hash(string plaintext)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));

    /// PHP's hash('crc32b'): the standard reflected CRC-32 (IEEE 802.3).
    public static uint Crc32b(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
