-- ============================================================================
--  门禁事件表 AcsEvents 建表脚本（SQLite）
--
--  用途：当 ISUP:EventStore:AutoCreate=false 时，由运维手工建表
--        （库文件取自 ISUP:EventStore:SqlitePath，默认 ./storage/acs-events.db）。
--  生成：由 EF Core 的 Database.GenerateCreateScript() 输出，与实体模型严格一致；
--        修改 AcsEventRecord / AcsEventDbContext 后需重新生成。
--  执行：sqlite3 ./storage/acs-events.db < docs/event-store-sqlite.sql
--        （库文件不存在时 sqlite3 会自动创建）
--
--  注意：UX_AcsEvents_DedupHash 唯一索引是"重复事件只落一行"的关键，不要省略。
-- ============================================================================
CREATE TABLE "AcsEvents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AcsEvents" PRIMARY KEY AUTOINCREMENT,
    "DedupKey" TEXT NOT NULL,
    "DedupHash" TEXT NOT NULL,
    "DeviceId" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "SerialNumber" TEXT NULL,
    "EventSerialNo" TEXT NULL,
    "EventType" TEXT NULL,
    "SubEventType" TEXT NULL,
    "Major" INTEGER NULL,
    "Minor" INTEGER NULL,
    "EmployeeNo" TEXT NULL,
    "CardNo" TEXT NULL,
    "PersonName" TEXT NULL,
    "DoorNo" INTEGER NULL,
    "VerifyMode" TEXT NULL,
    "AlarmTopic" TEXT NULL,
    "AlarmType" INTEGER NOT NULL,
    "DataType" INTEGER NOT NULL,
    "PayloadFormat" TEXT NOT NULL,
    "Payload" TEXT NULL,
    "PayloadBytes" INTEGER NOT NULL,
    "PayloadTruncated" INTEGER NOT NULL,
    "PictureFileNames" TEXT NULL,
    "IsAccessControl" INTEGER NOT NULL,
    "HttpUrl" TEXT NULL,
    "ListenHandle" INTEGER NULL,
    "EventTime" INTEGER NOT NULL,
    "ReceivedAt" INTEGER NOT NULL,
    "EnqueuedAt" INTEGER NOT NULL,
    "StoredAt" INTEGER NOT NULL,
    "QueueProvider" TEXT NULL,
    "QueueMessageId" TEXT NULL,
    "DeliveryAttempts" INTEGER NOT NULL
);


CREATE INDEX "IX_AcsEvents_Acs_EventTime" ON "AcsEvents" ("IsAccessControl", "EventTime");


CREATE INDEX "IX_AcsEvents_Device_EventTime" ON "AcsEvents" ("DeviceId", "EventTime");


CREATE INDEX "IX_AcsEvents_EmployeeNo" ON "AcsEvents" ("EmployeeNo");


CREATE INDEX "IX_AcsEvents_EventTime" ON "AcsEvents" ("EventTime");


CREATE INDEX "IX_AcsEvents_ReceivedAt" ON "AcsEvents" ("ReceivedAt");


CREATE UNIQUE INDEX "UX_AcsEvents_DedupHash" ON "AcsEvents" ("DedupHash");


