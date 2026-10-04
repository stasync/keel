using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Keel.Utils.Security
{
    /// <summary>
    /// AES-256-CBC encryption utility.
    /// </summary>
    public static class EncryptionUtility
    {
        private const int MIN_KEY_LENGTH = 8; // Minimum length for the secret key

        /// <summary>
        /// Encrypt data with a secret key.
        /// </summary>
        /// <param name="data">Plain text data to encrypt</param>
        /// <param name="secret">Secret key (must be at least 8 characters long, will be hashed to 32 bytes)</param>
        /// <returns>Encrypted data as base64 string with IV prepended (format: "iv: encrypted")</returns>
        public static string Encrypt(string data, string secret)
        {
            if (string.IsNullOrWhiteSpace(data))
                throw new ArgumentException("Data to encrypt cannot be null or empty", nameof(data));

            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("Secret key cannot be null or empty", nameof(secret));

            if (secret.Length < MIN_KEY_LENGTH)
                throw new ArgumentException($"Secret key must be at least '{MIN_KEY_LENGTH}' characters long", nameof(secret));

            using var sha256 = SHA256.Create();
            var key = sha256.ComputeHash(buffer: Encoding.UTF8.GetBytes(secret));

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

            return $"{Convert.ToBase64String(aes.IV)}:{Convert.ToBase64String(ms.ToArray())}";
        }

        /// <summary>
        /// Decrypt data with a secret key.
        /// </summary>
        /// <param name="encryptedData">Encrypted data with IV (format: "iv: encrypted")</param>
        /// <param name="secret">Secret key (must match the one used for encryption)</param>
        /// <returns>Decrypted plain text data</returns>
        /// <exception cref="ArgumentException">If encrypted data format is invalid</exception>
        /// <exception cref="CryptographicException">If decryption fails (wrong key, corrupted data, etc.)</exception>
        public static string Decrypt(string encryptedData, string secret)
        {
            if (string.IsNullOrWhiteSpace(encryptedData))
                throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("Secret key cannot be null or empty", nameof(secret));

            if (secret.Length < MIN_KEY_LENGTH)
                throw new ArgumentException($"Secret key must be at least '{MIN_KEY_LENGTH}' characters long", nameof(secret));

            var parts = encryptedData.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException("Invalid encrypted data format");

            using var sha256 = SHA256.Create();
            var key = sha256.ComputeHash(buffer: Encoding.UTF8.GetBytes(secret));
            var iv = Convert.FromBase64String(parts[0]);
            var encrypted = Convert.FromBase64String(parts[1]);

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
    }
}