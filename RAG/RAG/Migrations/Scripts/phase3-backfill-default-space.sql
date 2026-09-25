-- Phase 3 backfill：在 AddSpaceScope 之後、RequireSpaceScope 之前執行（可重複執行）。
-- 依核准的 backfill policy：建立單一共用 Space，所有帳號加入（@owner_user_id 為 owner，其餘 member），
-- 帳號若尚無預設 Space 則設為預設；所有尚未歸屬的聊天室與問題歸入此 Space。
-- 執行前請調整下列參數；請以 utf8mb4 連線執行（例如 mariadb --default-character-set=utf8mb4）。

SET @space_key = 'default';
SET @space_name = '預設空間';
SET @owner_user_id = 3;

START TRANSACTION;

INSERT INTO `Space` (`Key`, `Name`, `IsActive`, `CreateAt`)
SELECT @space_key, @space_name, 1, NOW()
WHERE NOT EXISTS (SELECT 1 FROM `Space` WHERE `Key` = @space_key);

SET @space_id = (SELECT `ID` FROM `Space` WHERE `Key` = @space_key);

INSERT INTO `AccountSpace` (`UserID`, `SpaceID`, `Role`, `IsActive`, `IsDefault`, `CreateAt`)
SELECT u.`ID`,
       @space_id,
       IF(u.`ID` = @owner_user_id, 'owner', 'member'),
       u.`IsActive`,
       NOT EXISTS (SELECT 1 FROM `AccountSpace` d WHERE d.`UserID` = u.`ID` AND d.`IsDefault` = 1),
       NOW()
FROM `UserCache` u
WHERE NOT EXISTS (SELECT 1 FROM `AccountSpace` a WHERE a.`UserID` = u.`ID` AND a.`SpaceID` = @space_id);

UPDATE `ChatHeader` SET `SpaceID` = @space_id WHERE `SpaceID` IS NULL;
UPDATE `Questions` SET `SpaceID` = @space_id WHERE `SpaceID` IS NULL;

COMMIT;

-- 驗證報告：unscoped 必須為 0 才可套用 RequireSpaceScope
SELECT 'space_id' AS item, @space_id AS value
UNION ALL SELECT 'memberships', COUNT(*) FROM `AccountSpace` WHERE `SpaceID` = @space_id
UNION ALL SELECT 'chat_in_space', COUNT(*) FROM `ChatHeader` WHERE `SpaceID` = @space_id
UNION ALL SELECT 'question_in_space', COUNT(*) FROM `Questions` WHERE `SpaceID` = @space_id
UNION ALL SELECT 'unscoped_chat', COUNT(*) FROM `ChatHeader` WHERE `SpaceID` IS NULL
UNION ALL SELECT 'unscoped_question', COUNT(*) FROM `Questions` WHERE `SpaceID` IS NULL;
