using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Keel.Utils.Security
{
    /// <summary>
    /// AES-256-CBC encryption with an HMAC-SHA256 integrity check (encrypt-then-MAC).
    /// Decrypt rejects data that was modified or encrypted with a different key.
    /// The format must stay in sync with crypto_utils.ts in lro_server, which creates the tokens.
    /// </summary>
    public static class EncryptionUtility
    {
        private const int MIN_KEY_LENGTH = 8; // Minimum length for the secret key
        private const string MAC_KEY_PREFIX = "mac:"; // Derives a separate HMAC key from the same secret

        /// <summary>
        /// Encrypt data with a secret key.
        /// </summary>
        /// <param name="data">Plain text data to encrypt</param>
        /// <param name="secret">Secret key (must be at least 8 characters long, will be hashed to 32 bytes)</param>
        /// <returns>Base64 parts separated by colons (format: "iv:ciphertext:mac")</returns>
        public static string Encrypt(string data, string secret)
        {
            if (string.IsNullOrWhiteSpace(data))
                throw new ArgumentException("Data to encrypt cannot be null or empty", nameof(data));

            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("Secret key cannot be null or empty", nameof(secret));

            if (secret.Length < MIN_KEY_LENGTH)
                throw new ArgumentException($"Secret key must be at least '{MIN_KEY_LENGTH}' characters long", nameof(secret));

            var key = DeriveKey(secret);
            var macKey = DeriveKey(MAC_KEY_PREFIX + secret);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var cryptoTransform = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write);
            using (var writer = new StreamWriter(cs))
            {
                writer.Write(data);
            }

            var iv = aes.IV;
            var ciphertext = ms.ToArray();
            var mac = ComputeMac(macKey, iv, ciphertext);
            return $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(ciphertext)}:{Convert.ToBase64String(mac)}";
        }

        /// <summary>
        /// Decrypt data with a secret key.
        /// </summary>
        /// <param name="encryptedData">Encrypted data (format: "iv:ciphertext:mac")</param>
        /// <param name="secret">Secret key (must match the one used for encryption)</param>
        /// <returns>Decrypted plain text data</returns>
        /// <exception cref="ArgumentException">If encrypted data format is invalid</exception>
        /// <exception cref="CryptographicException">If the integrity check fails (wrong key or modified data)</exception>
        public static string Decrypt(string encryptedData, string secret)
        {
            if (string.IsNullOrWhiteSpace(encryptedData))
                throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("Secret key cannot be null or empty", nameof(secret));

            if (secret.Length < MIN_KEY_LENGTH)
                throw new ArgumentException($"Secret key must be at least '{MIN_KEY_LENGTH}' characters long", nameof(secret));

            var parts = encryptedData.Split(':');
            if (parts.Length != 3)
                throw new ArgumentException("Invalid encrypted data format");

            var iv = FromBase64Strict(parts[0]);
            var encrypted = FromBase64Strict(parts[1]);
            var mac = FromBase64Strict(parts[2]);

            // Check the MAC before decrypting anything, so modified data is never processed.
            var key = DeriveKey(secret);
            var macKey = DeriveKey(MAC_KEY_PREFIX + secret);
            if (!CryptographicOperations.FixedTimeEquals(mac, ComputeMac(macKey, iv, encrypted)))
                throw new CryptographicException("Integrity check failed: wrong key or modified data");

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var cryptoTransform = aes.CreateDecryptor();
            using var ms = new MemoryStream(encrypted);
            using var cs = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            return reader.ReadToEnd();
        }

        private static byte[] DeriveKey(string secret)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(buffer: Encoding.UTF8.GetBytes(secret));
        }

        private static byte[] ComputeMac(byte[] macKey, byte[] iv, byte[] ciphertext)
        {
            using var hmac = new HMACSHA256(macKey);
            hmac.TransformBlock(iv, 0, iv.Length, null, 0);
            hmac.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            return hmac.Hash;
        }

        /// <summary>
        /// Convert.FromBase64String ignores whitespace, so " abc" and "abc" decode to the same bytes.
        /// Requiring the canonical form gives every valid token exactly one string representation,
        /// which callers can rely on, e.g. to reject a token that was already used.
        /// </summary>
        private static byte[] FromBase64Strict(string value)
        {
            var bytes = Convert.FromBase64String(value);
            if (Convert.ToBase64String(bytes) != value)
                throw new ArgumentException("Invalid encrypted data format");

            return bytes;
        }
    }
}