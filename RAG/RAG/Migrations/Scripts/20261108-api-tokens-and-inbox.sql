-- 應用程式權杖（ApiToken）與暫存區（KnowledgeDraft）。
--
-- 只新增兩張資料表，不改動既有資料表與資料；可重複執行（IF NOT EXISTS）。
-- 套用順序：先執行本腳本，再部署新版 API（新版啟動後就會查詢這兩張表）。
-- 請以 utf8mb4 連線執行（例如 mariadb --default-character-set=utf8mb4）。
--
-- 回復（會一併刪除所有權杖與草稿，已接受的草稿所產生的正式問答不受影響）：
--   DROP TABLE `KnowledgeDraft`;
--   DROP TABLE `ApiToken`;

CREATE TABLE IF NOT EXISTS `ApiToken` (
  `ID`          int(11)      NOT NULL AUTO_INCREMENT,
  `UserID`      int(11)      NOT NULL,
  `Name`        varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `TokenPrefix` varchar(16)  NOT NULL,
  `TokenHash`   varchar(64)  NOT NULL,
  `Scopes`      varchar(200) NOT NULL,
  `SpaceIDs`    varchar(200) NULL,
  `CreateAt`    datetime     NOT NULL,
  `ExpiresAt`   datetime     NULL,
  `LastUsedAt`  datetime     NULL,
  `RevokedAt`   datetime     NULL,
  PRIMARY KEY (`ID`),
  UNIQUE KEY `ux_apitoken_hash` (`TokenHash`),
  KEY `ix_apitoken_user` (`UserID`),
  CONSTRAINT `fk_apitoken_user` FOREIGN KEY (`UserID`) REFERENCES `UserCache` (`ID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `KnowledgeDraft` (
  `ID`                 int(11)       NOT NULL AUTO_INCREMENT,
  `SpaceID`            int(11)       NOT NULL,
  `UserID`             int(11)       NOT NULL,
  `Question`           text          CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Answer`             text          CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `QaItemTypeID`       int(11)       NULL,
  `Source`             varchar(100)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `SourceRef`          varchar(500)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
  `ContentHash`        varchar(64)   NOT NULL,
  `Status`             varchar(20)   NOT NULL DEFAULT 'Pending',
  `AcceptedQuestionID` int(11)       NULL,
  `CreateAt`           datetime      NOT NULL,
  `ReviewedAt`         datetime      NULL,
  `Version`            varchar(32)   NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `ix_draft_space_user_status` (`SpaceID`, `UserID`, `Status`),
  KEY `ix_draft_hash` (`SpaceID`, `UserID`, `ContentHash`),
  KEY `fk_draft_user` (`UserID`),
  CONSTRAINT `fk_draft_space` FOREIGN KEY (`SpaceID`) REFERENCES `Space` (`ID`),
  CONSTRAINT `fk_draft_user`  FOREIGN KEY (`UserID`)  REFERENCES `UserCache` (`ID`) ON DELETE CASCADE,
  CONSTRAINT `ck_draft_status` CHECK (`Status` IN ('Pending','Accepting','Accepted','Rejected'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
