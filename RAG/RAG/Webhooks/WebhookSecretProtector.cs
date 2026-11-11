using System.Security.Cryptography;
using System.Text;

namespace RAG.Webhooks
{
    public interface IWebhookSecretProtector
    {
        string Protect(string secret);

        string Unprotect(string protectedSecret);
    }

    /// <summary>
    /// 簽章密鑰必須以明文計算 HMAC，所以不能只存雜湊；改以 AES-GCM 加密後存放（金鑰由 JWT 簽章金鑰經 HKDF 衍生，
    /// 用途分離，不會與 JWT 簽章共用同一把）。資料庫外洩時，沒有伺服器的金鑰檔就無法還原密鑰。
    /// </summary>
    public sealed class WebhookSecretProtector : IWebhookSecretProtector
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private static readonly byte[] Info = Encoding.UTF8.GetBytes("rag:webhook-secret:v1");

        private readonly byte[] _key;

        public WebhookSecretProtector(RAG.Authentication.JwtOptions jwt)
        {
            _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, jwt.SigningKey, 32, salt: null, info: Info);
        }

        public string Protect(string secret)
        {
            var plaintext = Encoding.UTF8.GetBytes(secret);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, cipher, tag);
            return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
        }

        public string Unprotect(string protectedSecret)
        {
            var all = Convert.FromBase64String(protectedSecret);
            var nonce = all[..NonceSize];
            var tag = all[NonceSize..(NonceSize + TagSize)];
            var cipher = all[(NonceSize + TagSize)..];
            var plaintext = new byte[cipher.Length];
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
    }
}
