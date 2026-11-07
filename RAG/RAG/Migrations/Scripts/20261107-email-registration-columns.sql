-- 自行註冊（信箱帳號 + 密碼雜湊）：放寬 UserCache.Account / Password 的長度。
--   Account  varchar(12)  -> varchar(254)  自行註冊的帳號就是信箱（RFC 上限 254）
--   Password varchar(12)  -> varchar(255)  儲存 PBKDF2 雜湊（pbkdf2-sha512$迭代$salt$hash，約 130 字元）
--
-- 只放寬長度，不改任何資料；可重複執行。字元集 / 定序維持原本的 latin1_swedish_ci（不分大小寫比對帳號）。
-- 既有帳號的明文密碼不需要轉換：下次登入成功時，後端會自動升級為雜湊。
--
-- 套用順序：先執行本腳本，再部署新版 API（新版寫入超過 12 字元的值，舊欄位會被資料庫拒絕）。
-- 執行前請先備份 UserCache；請以 utf8mb4 連線執行（例如 mariadb --default-character-set=utf8mb4）。
--
-- 回復（只有在沒有任何超過 12 字元的帳號 / 密碼時才能執行，否則會被拒絕或截斷）：
--   ALTER TABLE `UserCache`
--     MODIFY `Account`  varchar(12) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
--     MODIFY `Password` varchar(12) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL;

ALTER TABLE `UserCache`
  MODIFY `Account`  varchar(254) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
  MODIFY `Password` varchar(255) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL;
