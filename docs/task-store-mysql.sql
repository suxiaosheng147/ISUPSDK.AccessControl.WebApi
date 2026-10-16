-- ============================================================================
--  异步下发任务表 AcsTasks 建表脚本（MySQL / MariaDB）
--
--  用途：服务启动时会自动 EnsureCreated 建库建表（无 AutoCreate 开关）；
--        本脚本供 DBA 预先建表、或核对/重建表结构使用。
--  生成：由 EF Core 的 Database.GenerateCreateScript() 输出（Pomelo provider），
--        与实体模型严格一致；修改实体后需重新生成。
--  执行：USE <你的库>;  然后  source docs/task-store-mysql.sql;
--
--  注意：老库升级时服务启动会自动补 PayloadFormat 列，无需人工处理。
-- ============================================================================
ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `AcsTasks` (
    `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
    `DeviceId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Entity` int NOT NULL,
    `Operation` int NOT NULL,
    `Status` int NOT NULL,
    `Payload` longtext CHARACTER SET utf8mb4 NULL,
    `PayloadFormat` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `BinaryPath` longtext CHARACTER SET utf8mb4 NULL,
    `TimeoutMs` int unsigned NOT NULL,
    `Attempts` int NOT NULL,
    `MaxAttempts` int NOT NULL,
    `DeviceSuccess` tinyint(1) NOT NULL,
    `DeviceStatusCode` int NOT NULL,
    `SubStatusCode` varchar(128) CHARACTER SET utf8mb4 NULL,
    `ResultJson` longtext CHARACTER SET utf8mb4 NULL,
    `Error` varchar(2048) CHARACTER SET utf8mb4 NULL,
    `CreatedAt` bigint NOT NULL,
    `StartedAt` bigint NULL,
    `CompletedAt` bigint NULL,
    `NextAttemptAt` bigint NOT NULL,
    `Description` varchar(512) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AcsTasks` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_AcsTasks_CreatedAt` ON `AcsTasks` (`CreatedAt`);


CREATE INDEX `IX_AcsTasks_DeviceId` ON `AcsTasks` (`DeviceId`);


CREATE INDEX `IX_AcsTasks_Status` ON `AcsTasks` (`Status`);


CREATE INDEX `IX_AcsTasks_Status_NextAttemptAt` ON `AcsTasks` (`Status`, `NextAttemptAt`);


