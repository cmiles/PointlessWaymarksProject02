using System.Security.Cryptography;
using System.Text;

namespace PointlessWaymarks.SiteViewerMaui.Tools;

// Copied (self-contained) from PointlessWaymarks.CommonTools.ObfuscationTools so this app stays
// independent of the other solution projects (see the S3/ copies for the same pattern).
public static class ObfuscationTools
{
    /// <summary>
    ///     AES String Decryption - the simplest way to use this is to only use the paired Encrypt/Decrypt
    ///     methods in this class. This class is entitled 'ObfuscationTools' in largely because the most common
    ///     use of these methods in the Pointless Waymarks codebase is with a key from a local Key Vault
    ///     - hopefully this naming helps to avoid an implication that anything using these methods is 'strongly
    ///     secure'.
    /// </summary>
    public static string Decrypt(this string textToDecrypt, string key)
    {
        //The basis for this code is: https://stackoverflow.com/questions/38795103/encrypt-string-in-net-core

        if (string.IsNullOrEmpty(textToDecrypt)) return string.Empty;

        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key must have valid value.", nameof(key));

        var combined = Convert.FromBase64String(textToDecrypt);
        var buffer = new byte[combined.Length];
        var aesKey = new byte[24];
        Buffer.BlockCopy(SHA512.HashData(Encoding.UTF8.GetBytes(key)), 0, aesKey, 0, 24);

        using var aes = Aes.Create() ?? throw new NullReferenceException("AES Object Creation Returned Null?");
        aes.Key = aesKey;

        var iv = new byte[aes.IV.Length];
        var cipherText = new byte[buffer.Length - iv.Length];

        Array.ConstrainedCopy(combined, 0, iv, 0, iv.Length);
        Array.ConstrainedCopy(combined, iv.Length, cipherText, 0, cipherText.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var resultStream = new MemoryStream();
        using (var aesStream = new CryptoStream(resultStream, decryptor, CryptoStreamMode.Write))
        using (var plainStream = new MemoryStream(cipherText))
        {
            plainStream.CopyTo(aesStream);
        }

        return Encoding.UTF8.GetString(resultStream.ToArray());
    }

    /// <summary>
    ///     AES String Encryption - the simplest way to use this is to only use the paired Encrypt/Decrypt
    ///     methods in this class. This class is entitled 'ObfuscationTools' in largely because the most common
    ///     use of these methods in the Pointless Waymarks codebase is with a key from a local Key Vault
    ///     - hopefully this naming helps to avoid an implication that anything using these methods is 'strongly
    ///     protected'.
    /// </summary>
    public static string Encrypt(this string textToEncrypt, string key)
    {
        //The basis for this code is: https://stackoverflow.com/questions/38795103/encrypt-string-in-net-core

        if (string.IsNullOrEmpty(textToEncrypt)) return string.Empty;

        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Key must have valid value.", nameof(key));

        var buffer = Encoding.UTF8.GetBytes(textToEncrypt);
        var aesKey = new byte[24];
        Buffer.BlockCopy(SHA512.HashData(Encoding.UTF8.GetBytes(key)), 0, aesKey, 0, 24);

        using var aes = Aes.Create() ?? throw new NullReferenceException("AES Object Creation Returned Null?");

        aes.Key = aesKey;

        using var decryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var resultStream = new MemoryStream();
        using (var aesStream = new CryptoStream(resultStream, decryptor, CryptoStreamMode.Write))
        using (var plainStream = new MemoryStream(buffer))
        {
            plainStream.CopyTo(aesStream);
        }

        var result = resultStream.ToArray();
        var combined = new byte[aes.IV.Length + result.Length];
        Array.ConstrainedCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Array.ConstrainedCopy(result, 0, combined, aes.IV.Length, result.Length);

        return Convert.ToBase64String(combined);
    }

    /// <summary>
    ///     Tries to Decrypt with the Decrypt method and in the case of an exception returns the failure message.
    ///     This ONLY catches exceptions - it does not attempt to validate any returned content from Decrypt!
    /// </summary>
    public static string TryDecrypt(this string textToDecrypt, string key,
        string failureMessage = "Error Decrypting Content?!?")
    {
        try
        {
            return textToDecrypt.Decrypt(key);
        }
        catch
        {
            return failureMessage;
        }
    }
}
