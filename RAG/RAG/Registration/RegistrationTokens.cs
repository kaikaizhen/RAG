using System.Security.Cryptography;
using System.Text;
using RAG.Authentication;

namespace RAG.Registration
{
    /// <summary>
    /// 驗證連結內攜帶的註冊資料。帳號在使用者點擊連結後才建立，因此不存在「待驗證帳號」可被搶先佔用。
    /// </summary>
    public sealed record RegistrationIntent(string Email, string PasswordHash, string NameZh, string NameEn, string NameOther);

    /// <summary>
    /// 驗證連結用權杖：AES-256-GCM 加密並認證（內容含密碼雜湊，不可明文出現在信件 / 網址 / log），
    /// 金鑰由 JWT 金鑰以 HKDF 衍生並綁定用途，因此不能拿來當 access / refresh token，反之亦然。
    /// </summary>
    public interface IRegistrationTokens
    {
        string Protect(RegistrationIntent intent, TimeSpan lifetime);

        bool TryUnprotect(string token, out RegistrationIntent intent);
    }

    public sealed class RegistrationTokens : IRegistrationTokens
    {
        private const int NonceBytes = 12;
        private const int TagBytes = 16;
        private const byte FormatVersion = 2;
        private const string HashPrefix = "pbkdf2-sha512$";

        // flags
        private const byte CompactHash = 1; // 雜湊以原始位元組儲存（比 base64 文字短）
        private const byte EnSameAsZh = 2;
        private const byte OtherSameAsEn = 4;

        private static readonly byte[] Aad = Encoding.UTF8.GetBytes("rag:registration:v2");

        private readonly byte[] _key;
        private readonly TimeProvider _time;

        public RegistrationTokens(JwtOptions jwt, TimeProvider time)
        {
            _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, jwt.SigningKey, 32, salt: null, info: Encoding.UTF8.GetBytes("rag:registration:v1"));
            _time = time;
        }

        public string Protect(RegistrationIntent intent, TimeSpan lifetime)
        {
            var plain = Pack(intent, _time.GetUtcNow().Add(lifetime).ToUnixTimeSeconds());
            var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagBytes];
            using (var aes = new AesGcm(_key, TagBytes)) aes.Encrypt(nonce, plain, cipher, tag, Aad);

            var packed = new byte[NonceBytes + cipher.Length + TagBytes];
            nonce.CopyTo(packed, 0);
            cipher.CopyTo(packed, NonceBytes);
            tag.CopyTo(packed, NonceBytes + cipher.Length);
            return Convert.ToBase64String(packed).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public bool TryUnprotect(string token, out RegistrationIntent intent)
        {
            intent = null!;
            if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;
            try
            {
                var padded = token.Replace('-', '+').Replace('_', '/');
                var packed = Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
                if (packed.Length <= NonceBytes + TagBytes) return false;

                var nonce = packed.AsSpan(0, NonceBytes);
                var cipher = packed.AsSpan(NonceBytes, packed.Length - NonceBytes - TagBytes);
                var tag = packed.AsSpan(packed.Length - TagBytes);
                var plain = new byte[cipher.Length];
                using (var aes = new AesGcm(_key, TagBytes)) aes.Decrypt(nonce, cipher, tag, plain, Aad);

                if (!TryUnpack(plain, out var parsed, out var expires)) return false;
                if (_time.GetUtcNow().ToUnixTimeSeconds() > expires) return false;
                intent = parsed;
                return true;
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException or EndOfStreamException or IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// 緊湊的二進位格式，讓驗證連結盡量短（信件備用網址、手機信箱都比較好看）：
        /// 版本 | 到期(uint32) | flags | 信箱 | 密碼雜湊 | 名稱（與前一個相同就省略）
        /// </summary>
        private static byte[] Pack(RegistrationIntent intent, long expires)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            var compact = TryParseHash(intent.PasswordHash, out var iterations, out var salt, out var hash);
            byte flags = 0;
            if (compact) flags |= CompactHash;
            if (intent.NameEn == intent.NameZh) flags |= EnSameAsZh;
            if (intent.NameOther == (intent.NameEn)) flags |= OtherSameAsEn;

            writer.Write(FormatVersion);
            writer.Write(checked((uint)expires));
            writer.Write(flags);
            writer.Write(intent.Email);
            if (compact)
            {
                writer.Write(iterations);
                writer.Write((byte)salt.Length);
                writer.Write(salt);
                writer.Write((byte)hash.Length);
                writer.Write(hash);
            }
            else
            {
                writer.Write(intent.PasswordHash);
            }
            writer.Write(intent.NameZh);
            if ((flags & EnSameAsZh) == 0) writer.Write(intent.NameEn);
            if ((flags & OtherSameAsEn) == 0) writer.Write(intent.NameOther);
            writer.Flush();
            return stream.ToArray();
        }

        private static bool TryUnpack(byte[] plain, out RegistrationIntent intent, out long expires)
        {
            intent = null!;
            expires = 0;
            using var reader = new BinaryReader(new MemoryStream(plain), Encoding.UTF8);
            if (reader.ReadByte() != FormatVersion) return false;
            expires = reader.ReadUInt32();
            var flags = reader.ReadByte();
            var email = reader.ReadString();

            string passwordHash;
            if ((flags & CompactHash) != 0)
            {
                var iterations = reader.ReadInt32();
                var salt = reader.ReadBytes(reader.ReadByte());
                var hash = reader.ReadBytes(reader.ReadByte());
                passwordHash = $"{HashPrefix}{iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
            }
            else
            {
                passwordHash = reader.ReadString();
            }

            var nameZh = reader.ReadString();
            var nameEn = (flags & EnSameAsZh) != 0 ? nameZh : reader.ReadString();
            var nameOther = (flags & OtherSameAsEn) != 0 ? nameEn : reader.ReadString();
            if (reader.BaseStream.Position != reader.BaseStream.Length) return false; // 不允許尾端多餘資料

            intent = new RegistrationIntent(email, passwordHash, nameZh, nameEn, nameOther);
            return true;
        }

        /// <summary>
        /// 解析 Pbkdf2PasswordHasher 的格式（pbkdf2-sha512$迭代$salt$hash）；不符合就退回以文字儲存
        /// </summary>
        private static bool TryParseHash(string value, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0;
            salt = hash = Array.Empty<byte>();
            if (!value.StartsWith(HashPrefix, StringComparison.Ordinal)) return false;
            var parts = value[HashPrefix.Length..].Split('$');
            if (parts.Length != 3 || !int.TryParse(parts[0], out iterations) || iterations < 1) return false;
            try
            {
                salt = Convert.FromBase64String(parts[1]);
                hash = Convert.FromBase64String(parts[2]);
            }
            catch (FormatException)
            {
                return false;
            }
            return salt.Length is > 0 and <= 255 && hash.Length is > 0 and <= 255
                && Convert.ToBase64String(salt) == parts[1] && Convert.ToBase64String(hash) == parts[2]; // 必須能無損還原
        }
    }
}
