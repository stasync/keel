using Keel.Utils.Math;
using Keel.Utils.Security;
using System.Security.Cryptography;
using Xunit.Abstractions;

namespace Keel.Utils.Tests
{
    public class EncryptionUtilityTests(ITestOutputHelper output)
    {
        [Fact]
        public void CorrectKeyTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "Hello World!";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine(encrypted);
            var decrypted = EncryptionUtility.Decrypt(encrypted, secret: key);
            output.WriteLine(decrypted);
            Assert.Equal(message, decrypted);
        }

        [Fact]
        public void IncorrectKeyTest()
        {
            const string encryptionKey = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "Hello World!";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: encryptionKey);
            output.WriteLine(encrypted);

            // This key is slightly different.
            const string decryptionKey = "zLtb9s1hNltZl6glbHliS66enhD42Fs3";
            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Decrypt(encrypted, secret: decryptionKey));
        }

        /// <summary>
        /// Changing the IV used to go unnoticed: Decrypt succeeded and returned different text.
        /// The MAC check now rejects it.
        /// </summary>
        [Fact]
        public void IncorrectTokenTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "Hello World!";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine(encrypted);

            var encryptedCharArray = encrypted.ToCharArray();
            const string replacePool = "abcdefghijklmnopqrstuvwxyzABCDEFGHJKLMNOPQRSTUVWXYZ0123456789";
            foreach (var c in replacePool)
            {
                if (encryptedCharArray[0] == c)
                    continue;

                encryptedCharArray[0] = c;
                break;
            }

            var changedEncrypted = new string(encryptedCharArray);
            output.WriteLine(changedEncrypted);

            Assert.Throws<CryptographicException>(() => EncryptionUtility.Decrypt(changedEncrypted, secret: key));
        }

        [Fact]
        public void IncorrectTokenFormatTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "Hello World!";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine(encrypted);

            var changedEncrypted = RandomExtended.Default.String(encrypted.Length);
            output.WriteLine(changedEncrypted);
            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Decrypt(changedEncrypted, secret: key));
        }

        [Fact]
        public void LargeMessageTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var message = new string('A', 10000); // 10KB of 'A' characters
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine($"Encrypted message length: {encrypted.Length}");
            var decrypted = EncryptionUtility.Decrypt(encrypted, secret: key);
            Assert.Equal(message, decrypted);
            Assert.Equal(10000, decrypted.Length);
        }

        [Fact]
        public void SpecialCharactersTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "!@#$%^&*()_+-=[]{}|;':\",./<>?~`\n\r\t";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine($"Encrypted special chars: {encrypted}");
            var decrypted = EncryptionUtility.Decrypt(encrypted, secret: key);
            output.WriteLine($"Decrypted: {decrypted}");
            Assert.Equal(message, decrypted);
        }

        [Fact]
        public void UnicodeCharactersTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "Hello 世界! 🌍 émojis 日本語 Привет";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine($"Encrypted unicode: {encrypted}");
            var decrypted = EncryptionUtility.Decrypt(encrypted, secret: key);
            output.WriteLine($"Decrypted: {decrypted}");
            Assert.Equal(message, decrypted);
        }

        [Fact]
        public void JsonPayloadTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "{\"TargetPort\":9000,\"IssueUnixTime\":1737374096789,\"ExpirationUnixTime\":1737374126789}";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
            output.WriteLine($"Encrypted JSON: {encrypted}");
            var decrypted = EncryptionUtility.Decrypt(encrypted, secret: key);
            output.WriteLine($"Decrypted JSON: {decrypted}");
            Assert.Equal(message, decrypted);
        }

        [Fact]
        public void MultipleEncryptionsSameMessageTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "Same message encrypted multiple times";

            var encrypted1 = EncryptionUtility.Encrypt(data: message, secret: key);
            var encrypted2 = EncryptionUtility.Encrypt(data: message, secret: key);
            var encrypted3 = EncryptionUtility.Encrypt(data: message, secret: key);

            output.WriteLine($"Encryption 1: {encrypted1}");
            output.WriteLine($"Encryption 2: {encrypted2}");
            output.WriteLine($"Encryption 3: {encrypted3}");

            // Encrypted values should be different due to random IV
            Assert.NotEqual(encrypted1, encrypted2);
            Assert.NotEqual(encrypted2, encrypted3);

            // But all should decrypt to the same message
            Assert.Equal(message, EncryptionUtility.Decrypt(encrypted1, secret: key));
            Assert.Equal(message, EncryptionUtility.Decrypt(encrypted2, secret: key));
            Assert.Equal(message, EncryptionUtility.Decrypt(encrypted3, secret: key));
        }

        [Fact]
        public void ShortKeyTest()
        {
            const string shortKey = "short";
            const string message = "Test message";

            // Should throw exception for key that's too short
            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Encrypt(data: message, secret: shortKey));
        }

        [Fact]
        public void NullMessageTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";

            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Encrypt(data: null, secret: key));
        }

        [Fact]
        public void NullKeyTest()
        {
            const string message = "Test message";

            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Encrypt(data: message, secret: null));
        }

        [Fact]
        public void WhitespaceMessageTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "   \t\n   ";
            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Encrypt(data: message, secret: key));
        }

        [Fact]
        public void RoundTripMultipleMessagesTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var messages = new[]
            {
                "First message",
                "Second message with more content",
                "123456789",
                "Mixed content: 123 ABC !@#",
                "Short"
            };

            foreach (var message in messages)
            {
                var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);
                var decrypted = EncryptionUtility.Decrypt(encrypted, secret: key);
                output.WriteLine($"Original: '{message}' -> Encrypted: '{encrypted}' -> Decrypted: '{decrypted}'");
                Assert.Equal(message, decrypted);
            }
        }

        [Fact]
        public void TruncatedEncryptedDataTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "This will be truncated";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);

            // Truncate the encrypted string
            var truncated = encrypted.Substring(0, encrypted.Length / 2);
            output.WriteLine($"Original encrypted: {encrypted}");
            output.WriteLine($"Truncated: {truncated}");

            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Decrypt(truncated, secret: key));
        }

        [Fact]
        public void DifferentKeySameLengthTest()
        {
            const string message = "Test message for different keys";
            const string key1 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string key2 = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

            var encrypted1 = EncryptionUtility.Encrypt(data: message, secret: key1);
            var encrypted2 = EncryptionUtility.Encrypt(data: message, secret: key2);

            output.WriteLine($"Encrypted with key1: {encrypted1}");
            output.WriteLine($"Encrypted with key2: {encrypted2}");

            // Different keys should produce different encrypted outputs
            Assert.NotEqual(encrypted1, encrypted2);

            // Each can only be decrypted with its own key
            Assert.Equal(message, EncryptionUtility.Decrypt(encrypted1, secret: key1));
            Assert.Equal(message, EncryptionUtility.Decrypt(encrypted2, secret: key2));

            // Cross-decryption should fail
            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Decrypt(encrypted1, secret: key2));
            Assert.ThrowsAny<Exception>(() => EncryptionUtility.Decrypt(encrypted2, secret: key1));
        }

        [Fact]
        public void FormatTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var encrypted = EncryptionUtility.Encrypt(data: "Hello World!", secret: key);
            output.WriteLine(encrypted);

            var parts = encrypted.Split(':');
            Assert.Equal(3, parts.Length);
            Assert.Equal(16, Convert.FromBase64String(parts[0]).Length); // IV
            Assert.Equal(32, Convert.FromBase64String(parts[2]).Length); // HMAC-SHA256
        }

        /// <summary>
        /// The attack the MAC exists to stop: relay tokens start with {"TargetPort":90, and XOR-ing the IV rewrites
        /// those bytes without knowing the key. Before the integrity check, this turned port 9000 into 7700.
        /// </summary>
        [Fact]
        public void ForgedTokenTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string message = "{\"TargetPort\":9000,\"IssueUnixTime\":1737374096789,\"ExpirationUnixTime\":1737374126789}";
            var encrypted = EncryptionUtility.Encrypt(data: message, secret: key);

            var parts = encrypted.Split(':');
            var iv = Convert.FromBase64String(parts[0]);
            iv[14] ^= (byte)('9' ^ '7');
            iv[15] ^= (byte)('0' ^ '7');
            var forged = $"{Convert.ToBase64String(iv)}:{parts[1]}:{parts[2]}";
            output.WriteLine(forged);

            Assert.Throws<CryptographicException>(() => EncryptionUtility.Decrypt(forged, secret: key));
        }

        [Theory]
        [InlineData(0)] // IV
        [InlineData(1)] // Ciphertext
        [InlineData(2)] // MAC
        public void FlippedBitTest(int partIndex)
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var encrypted = EncryptionUtility.Encrypt(data: "Hello World!", secret: key);

            var parts = encrypted.Split(':');
            var bytes = Convert.FromBase64String(parts[partIndex]);
            bytes[^1] ^= 1;
            parts[partIndex] = Convert.ToBase64String(bytes);
            var tampered = string.Join(":", parts);
            output.WriteLine(tampered);

            Assert.Throws<CryptographicException>(() => EncryptionUtility.Decrypt(tampered, secret: key));
        }

        /// <summary>
        /// Without a MAC, about 1 in 300 wrong keys happened to produce valid padding and "decrypted" to garbage.
        /// </summary>
        [Fact]
        public void WrongKeyAlwaysFailsTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var encrypted = EncryptionUtility.Encrypt(data: "Hello World!", secret: key);

            for (var i = 0; i < 1000; i++)
            {
                var wrongKey = $"wrong-key-{i:D4}";
                Assert.Throws<CryptographicException>(() => EncryptionUtility.Decrypt(encrypted, secret: wrongKey));
            }
        }

        /// <summary>
        /// Base64 decoding ignores whitespace, so a respaced copy of a token would decrypt to the same content while
        /// being a different string - enough to get past a "token already used" check that compares strings.
        /// </summary>
        [Fact]
        public void WhitespaceInsertedTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var encrypted = EncryptionUtility.Encrypt(data: "Hello World!", secret: key);

            var respaced = encrypted.Replace(":", ": ");
            output.WriteLine(respaced);

            Assert.Throws<ArgumentException>(() => EncryptionUtility.Decrypt(respaced, secret: key));
        }

        /// <summary>
        /// Tokens in the old "iv:ciphertext" format have no MAC, so they can't be trusted and are rejected.
        /// </summary>
        [Fact]
        public void LegacyFormatTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            var encrypted = EncryptionUtility.Encrypt(data: "Hello World!", secret: key);

            var parts = encrypted.Split(':');
            var legacy = $"{parts[0]}:{parts[1]}";

            Assert.Throws<ArgumentException>(() => EncryptionUtility.Decrypt(legacy, secret: key));
        }

        /// <summary>
        /// Token produced by encrypt() in lro_server's crypto_utils.ts. If this fails, the two implementations have
        /// drifted apart and the relay will reject every token the Node service issues.
        /// </summary>
        [Fact]
        public void CryptoUtilsTsTokenTest()
        {
            const string key = "XLtb9sJhNltZl6JlbHliS66eXhD42Fs3";
            const string token = "ZO8VHk7/Z7CPeBMbxEe3kg==:HVCY1Vnq/YETuG4TD8tqmZ988xGUYwLL8yz4/ctE8TmSRW2YKwNp4ajbqFNjjEseNeveI3llhmBAdPzDm/3sWe+8FL9MaI29uUXEK8nAqossKsQF961ZqkVKlA0R0+WU:qY117npdx1dJmWMwa0kCaRdJBpx8DYrP+lB1VaBAhJg=";
            const string message = "{\"TargetPort\":9000,\"IssueUnixTime\":1737374096789,\"ExpirationUnixTime\":1737374126789}";

            Assert.Equal(message, EncryptionUtility.Decrypt(token, secret: key));
        }
    }
}