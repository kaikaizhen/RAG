-- Webhook：使用者設定的通知網址（Webhook）與送出紀錄（WebhookDelivery，outbox）。
--
-- 只新增兩張資料表，不改動既有資料表與資料；可重複執行（IF NOT EXISTS）。
-- 前置：先套用 20261108-api-tokens-and-inbox.sql（本檔不依賴它的資料表，但部署的是同一批功能）。
-- 套用順序：先執行本腳本，再部署新版 API（新版的背景程式啟動後就會查詢這兩張表）。
-- 請以 utf8mb4 連線執行（例如 mariadb --default-character-set=utf8mb4）。
--
-- 回復（會一併刪除所有 webhook 設定與送出紀錄）：
--   DROP TABLE `WebhookDelivery`;
--   DROP TABLE `Webhook`;

CREATE TABLE IF NOT EXISTS `Webhook` (
  `ID`                  int(11)      NOT NULL AUTO_INCREMENT,
  `UserID`              int(11)      NOT NULL,
  `Name`                varchar(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Url`                 varchar(500) NOT NULL,
  `SecretProtected`     varchar(200) NOT NULL,
  `Events`              varchar(200) NOT NULL,
  `IsActive`            tinyint(1)   NOT NULL DEFAULT 1,
  `ConsecutiveFailures` int(11)      NOT NULL DEFAULT 0,
  `DisabledReason`      varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
  `CreateAt`            datetime     NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `ix_webhook_user` (`UserID`),
  CONSTRAINT `fk_webhook_user` FOREIGN KEY (`UserID`) REFERENCES `UserCache` (`ID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `WebhookDelivery` (
  `ID`            int(11)      NOT NULL AUTO_INCREMENT,
  `WebhookID`     int(11)      NOT NULL,
  `EventID`       varchar(36)  NOT NULL,
  `Event`         varchar(40)  NOT NULL,
  `Payload`       text         CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Status`        varchar(20)  NOT NULL DEFAULT 'Pending',
  `Attempts`      int(11)      NOT NULL DEFAULT 0,
  `NextAttemptAt` datetime     NULL,
  `LastStatusCode` int(11)     NULL,
  `LastError`     varchar(300) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL,
  `CreateAt`      datetime     NOT NULL,
  `DeliveredAt`   datetime     NULL,
  PRIMARY KEY (`ID`),
  KEY `ix_delivery_due` (`Status`, `NextAttemptAt`),
  KEY `ix_delivery_webhook` (`WebhookID`, `CreateAt`),
  CONSTRAINT `fk_delivery_webhook` FOREIGN KEY (`WebhookID`) REFERENCES `Webhook` (`ID`) ON DELETE CASCADE,
  CONSTRAINT `ck_delivery_status` CHECK (`Status` IN ('Pending','Succeeded','Dead'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
