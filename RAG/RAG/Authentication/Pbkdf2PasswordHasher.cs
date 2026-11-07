using System.Security.Cryptography;
using System.Text;

namespace RAG.Authentication
{
    /// <summary>
    /// 密碼雜湊與驗證。格式：pbkdf2-sha512$迭代次數$salt(base64)$hash(base64)。
    /// 沒有此前綴的儲存值視為舊版明文，驗證成功後由呼叫端改存雜湊（lazy upgrade）。
    /// </summary>
    public interface IPasswordHasher
    {
        string Hash(string password);

        /// <summary>
        /// 驗證密碼；needsUpgrade 為 true 代表儲存值是舊明文或參數已過時，應重新雜湊後存回
        /// </summary>
        bool Verify(string password, string stored, out bool needsUpgrade);

        /// <summary>
        /// 帳號不存在時也做一次等量運算，避免以回應時間探測帳號是否存在
        /// </summary>
        void BurnVerification(string password);
    }

    public sealed class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const string Prefix = "pbkdf2-sha512$";
        private const int Iterations = 210_000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        /// <summary>
        /// 匿名端點會收到任意長度的密碼，限制上限避免被拿來做 CPU / 記憶體消耗
        /// </summary>
        public const int MaxPasswordLength = 128;

        private static readonly string DummyHash = new Pbkdf2PasswordHasher().Hash("dummy-password-for-timing");

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password, salt, Iterations);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        public bool Verify(string password, string stored, out bool needsUpgrade)
        {
            needsUpgrade = false;
            if (password.Length > MaxPasswordLength) return false;

            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                // 舊版明文：以固定時間比較
                var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(stored));
                needsUpgrade = ok;
                return ok;
            }

            var parts = stored[Prefix.Length..].Split('$');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations < 1) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                var actual = Derive(password, salt, iterations, expected.Length);
                var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
                needsUpgrade = ok && iterations < Iterations;
                return ok;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public void BurnVerification(string password) => Verify(password, DummyHash, out _);

        private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
            Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, length);
    }
}
