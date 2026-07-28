using System.Security.Cryptography;
using System.Text;

namespace PortfolioApi.Security;

/// <summary>
/// Authenticated field-level encryption for PII stored at rest (AES-256-GCM).
/// The key is derived (SHA-256) from a configured secret so a leaked database
/// dump exposes no birth details / contacts without the key. Output format:
/// base64( nonce[12] || tag[16] || ciphertext ).
/// </summary>
public static class FieldCrypto
{
    public static string Encrypt(string? plain, string secret)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] pt = Encoding.UTF8.GetBytes(plain);
        byte[] ct = new byte[pt.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, pt, ct, tag);
        byte[] outBuf = new byte[12 + 16 + ct.Length];
        Buffer.BlockCopy(nonce, 0, outBuf, 0, 12);
        Buffer.BlockCopy(tag, 0, outBuf, 12, 16);
        Buffer.BlockCopy(ct, 0, outBuf, 28, ct.Length);
        return Convert.ToBase64String(outBuf);
    }

    public static string Decrypt(string? cipher, string secret)
    {
        if (string.IsNullOrEmpty(cipher)) return string.Empty;
        try
        {
            byte[] raw = Convert.FromBase64String(cipher);
            if (raw.Length < 28) return string.Empty;
            byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
            byte[] nonce = raw[..12];
            byte[] tag = raw[12..28];
            byte[] ct = raw[28..];
            byte[] pt = new byte[ct.Length];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, ct, tag, pt);
            return Encoding.UTF8.GetString(pt);
        }
        catch
        {
            return "[decrypt-error]";
        }
    }
}
