using System.Net;
using System.Net.Mail;
using Library.Http;
using Library.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RAG.Authentication;
using RAG.Models.Entities;
using RAG.SpaceRuntime;

namespace RAG.Registration
{
    public interface IRegistrationService
    {
        /// <summary>
        /// 受理註冊：寄出驗證信。對呼叫端的結果一律相同（不論信箱是否已註冊），避免被用來探測帳號。
        /// </summary>
        Task RegisterAsync(string account, string password, string? nameZh, string? nameEn, string? nameOther, CancellationToken cancellationToken = default);

        /// <summary>
        /// 驗證連結：建立帳號與個人 Space。重複點擊（帳號已存在）視為成功。
        /// </summary>
        Task VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    }

    public sealed class RegistrationService : IRegistrationService
    {
        private const int MaxEmailLength = 254;
        private const int MaxNameLength = 50;

        private readonly WenDaHubContext _db;
        private readonly RegistrationOptions _options;
        private readonly IPasswordHasher _hasher;
        private readonly IRegistrationTokens _tokens;
        private readonly IRegistrationMailer _mailer;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(
            WenDaHubContext db,
            RegistrationOptions options,
            IPasswordHasher hasher,
            IRegistrationTokens tokens,
            IRegistrationMailer mailer,
            IMemoryCache cache,
            ILogger<RegistrationService> logger)
        {
            _db = db;
            _options = options;
            _hasher = hasher;
            _tokens = tokens;
            _mailer = mailer;
            _cache = cache;
            _logger = logger;
        }

        public async Task RegisterAsync(string account, string password, string? nameZh, string? nameEn, string? nameOther, CancellationToken cancellationToken = default)
        {
            EnsureOpen();

            var email = NormalizeEmail(account);
            if (password is null || password.Length < _options.PasswordMinLength)
                throw new HttpException(HttpStatusCode.BadRequest, $"密碼長度至少需要 {_options.PasswordMinLength} 個字元");
            if (password.Length > Pbkdf2PasswordHasher.MaxPasswordLength)
                throw new HttpException(HttpStatusCode.BadRequest, $"密碼長度不可超過 {Pbkdf2PasswordHasher.MaxPasswordLength} 個字元");

            // 同一信箱在冷卻期內只處理一次：不重複寄信，也讓回應時間不隨信箱狀態變化
            if (_cache.TryGetValue(CooldownKey(email), out _)) return;
            _cache.Set(CooldownKey(email), true, TimeSpan.FromSeconds(_options.ResendCooldownSeconds));

            try
            {
                if (await _db.UserCache.AsNoTracking().AnyAsync(u => u.Account == email, cancellationToken))
                {
                    await _mailer.SendAlreadyRegisteredAsync(email, cancellationToken);
                    return;
                }

                var fallback = email[..email.IndexOf('@')];
                var zh = Clean(nameZh) ?? Clean(nameEn) ?? Clean(nameOther) ?? fallback;
                var en = Clean(nameEn) ?? Clean(nameOther) ?? zh;
                var other = Clean(nameOther) ?? en;

                var lifetime = TimeSpan.FromHours(_options.TokenLifetimeHours);
                var token = _tokens.Protect(new RegistrationIntent(email, _hasher.Hash(password), zh, en, other), lifetime);
                var link = $"{_options.VerifyUrlBase}{(_options.VerifyUrlBase.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(token)}";
                await _mailer.SendVerificationAsync(email, zh, link, lifetime, cancellationToken);
            }
            catch (MailDeliveryException)
            {
                // 寄信失敗讓使用者可以立即重試，不要把他卡在冷卻期
                _cache.Remove(CooldownKey(email));
                throw new HttpException(HttpStatusCode.ServiceUnavailable, "驗證信寄送失敗，請稍後再試");
            }
        }

        public async Task VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
        {
            EnsureOpen();

            if (!_tokens.TryUnprotect(token, out var intent))
                throw new HttpException(HttpStatusCode.BadRequest, "驗證連結無效或已過期，請重新註冊");

            if (await _db.UserCache.AsNoTracking().AnyAsync(u => u.Account == intent.Email, cancellationToken)) return;

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

                var user = new UserCache
                {
                    Account = intent.Email,
                    Password = intent.PasswordHash,
                    NameZh = intent.NameZh,
                    NameEn = intent.NameEn,
                    NameOther = intent.NameOther,
                    Manage = "free",
                    IsActive = true,
                };
                _db.UserCache.Add(user);
                await _db.SaveChangesAsync(cancellationToken);

                // 每個自行註冊的帳號擁有自己的 Space：知識庫、對話都以 Space 隔離，不會與其他使用者互相污染
                var space = new Space
                {
                    Key = $"u-{user.ID}",
                    Name = Truncate($"{intent.NameZh} 的空間", 100),
                    IsActive = true,
                    CreateAt = DateTime.Now,
                };
                space.AccountSpace.Add(new AccountSpace
                {
                    UserID = user.ID,
                    Role = SpaceRoles.Owner,
                    IsActive = true,
                    IsDefault = true,
                    CreateAt = DateTime.Now,
                });
                _db.Space.Add(space);
                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // 同一連結被同時點兩次：另一個請求已建立帳號，視為成功
                _logger.LogInformation(ex, "Registration verification raced with another request.");
                _db.ChangeTracker.Clear();
                if (!await _db.UserCache.AsNoTracking().AnyAsync(u => u.Account == intent.Email, cancellationToken)) throw;
            }
        }

        private void EnsureOpen()
        {
            if (!_options.Enabled || !_mailer.Available)
                throw new HttpException(HttpStatusCode.ServiceUnavailable, "目前未開放註冊");
        }

        private static string NormalizeEmail(string account)
        {
            var value = (account ?? string.Empty).Trim();
            // Account 欄位為 latin1：只接受 ASCII 信箱，避免非 ASCII 字元在寫入時才被資料庫拒絕
            if (value.Length == 0 || value.Length > MaxEmailLength || value.Any(c => c > 127) || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value || !parsed.Host.Contains('.'))
                throw new HttpException(HttpStatusCode.BadRequest, "請輸入有效的電子信箱");
            return value.ToLowerInvariant();
        }

        private static string? Clean(string? name)
        {
            var value = name?.Trim();
            return string.IsNullOrEmpty(value) ? null : Truncate(value, MaxNameLength);
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

        private static string CooldownKey(string email) => $"register:{email}";
    }
}
