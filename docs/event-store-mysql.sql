-- ============================================================================
--  门禁事件表 AcsEvents 建表脚本（MySQL / MariaDB）
--
--  用途：当 ISUP:EventStore:AutoCreate=false 时，由运维手工建表。
--  生成：由 EF Core 的 Database.GenerateCreateScript() 输出（Pomelo provider），
--        与实体模型严格一致；修改实体后需重新生成。
--  执行：USE <你的库>;  然后  source docs/event-store-mysql.sql;
--
--  注意：UX_AcsEvents_DedupHash 唯一索引是"重复事件只落一行"的关键，不要省略。
-- ============================================================================
ALTER DATABASE CHARACTER SET utf8mb4;


CREATE TABLE `AcsEvents` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DedupKey` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `DedupHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `DeviceId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Source` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `SerialNumber` varchar(128) CHARACTER SET utf8mb4 NULL,
    `EventSerialNo` varchar(64) CHARACTER SET utf8mb4 NULL,
    `EventType` varchar(128) CHARACTER SET utf8mb4 NULL,
    `SubEventType` varchar(64) CHARACTER SET utf8mb4 NULL,
    `Major` int NULL,
    `Minor` int NULL,
    `EmployeeNo` varchar(64) CHARACTER SET utf8mb4 NULL,
    `CardNo` varchar(64) CHARACTER SET utf8mb4 NULL,
    `PersonName` varchar(128) CHARACTER SET utf8mb4 NULL,
    `DoorNo` int NULL,
    `VerifyMode` varchar(64) CHARACTER SET utf8mb4 NULL,
    `AlarmTopic` varchar(256) CHARACTER SET utf8mb4 NULL,
    `AlarmType` int NOT NULL,
    `DataType` int NOT NULL,
    `PayloadFormat` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `Payload` longtext CHARACTER SET utf8mb4 NULL,
    `PayloadBytes` int NOT NULL,
    `PayloadTruncated` tinyint(1) NOT NULL,
    `PictureFileNames` longtext CHARACTER SET utf8mb4 NULL,
    `IsAccessControl` tinyint(1) NOT NULL,
    `HttpUrl` varchar(512) CHARACTER SET utf8mb4 NULL,
    `ListenHandle` int NULL,
    `EventTime` bigint NOT NULL,
    `ReceivedAt` bigint NOT NULL,
    `EnqueuedAt` bigint NOT NULL,
    `StoredAt` bigint NOT NULL,
    `QueueProvider` varchar(32) CHARACTER SET utf8mb4 NULL,
    `QueueMessageId` varchar(128) CHARACTER SET utf8mb4 NULL,
    `DeliveryAttempts` int NOT NULL,
    CONSTRAINT `PK_AcsEvents` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_AcsEvents_Acs_EventTime` ON `AcsEvents` (`IsAccessControl`, `EventTime`);


CREATE INDEX `IX_AcsEvents_Device_EventTime` ON `AcsEvents` (`DeviceId`, `EventTime`);


CREATE INDEX `IX_AcsEvents_EmployeeNo` ON `AcsEvents` (`EmployeeNo`);


CREATE INDEX `IX_AcsEvents_EventTime` ON `AcsEvents` (`EventTime`);


CREATE INDEX `IX_AcsEvents_ReceivedAt` ON `AcsEvents` (`ReceivedAt`);


CREATE UNIQUE INDEX `UX_AcsEvents_DedupHash` ON `AcsEvents` (`DedupHash`);


