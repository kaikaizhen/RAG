-- 既有資料庫（已含 UserCache / ChatHeader / ChatLog / QaType / QaItemType / Questions）導入 EF Core migrations 時執行一次。
-- 只登記 Baseline 為已套用，不建立或修改任何既有資料表；可重複執行。
-- 之後以 `dotnet ef database update` 套用後續 migration（例如 AddSpaceRuntime）。

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260924175203_Baseline', '9.0.9');
