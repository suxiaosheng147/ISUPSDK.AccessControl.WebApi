-- ============================================================================
--  异步下发任务表 AcsTasks 建表脚本（SQLite）
--
--  用途：服务启动时会自动 EnsureCreated 建库建表（无 AutoCreate 开关）；
--        本脚本供 DBA 预先建表、或核对/重建表结构使用。
--        库文件取自 ISUP:TaskQueue:DatabasePath，默认 ./storage/acs-tasks.db。
--  生成：由 EF Core 的 Database.GenerateCreateScript() 输出，与实体模型严格一致；
--        修改 AcsTask / AcsTaskDbContext 后需重新生成。
--  执行：sqlite3 ./storage/acs-tasks.db < docs/task-store-sqlite.sql
--        （库文件不存在时 sqlite3 会自动创建）
--
--  注意：老库升级时服务启动会自动补 PayloadFormat 列，无需人工处理。
-- ============================================================================
CREATE TABLE "AcsTasks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AcsTasks" PRIMARY KEY,
    "DeviceId" TEXT NOT NULL,
    "Entity" INTEGER NOT NULL,
    "Operation" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "Payload" TEXT NULL,
    "PayloadFormat" TEXT NOT NULL,
    "BinaryPath" TEXT NULL,
    "TimeoutMs" INTEGER NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "MaxAttempts" INTEGER NOT NULL,
    "DeviceSuccess" INTEGER NOT NULL,
    "DeviceStatusCode" INTEGER NOT NULL,
    "SubStatusCode" TEXT NULL,
    "ResultJson" TEXT NULL,
    "Error" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "StartedAt" INTEGER NULL,
    "CompletedAt" INTEGER NULL,
    "NextAttemptAt" INTEGER NOT NULL,
    "Description" TEXT NULL
);


CREATE INDEX "IX_AcsTasks_CreatedAt" ON "AcsTasks" ("CreatedAt");


CREATE INDEX "IX_AcsTasks_DeviceId" ON "AcsTasks" ("DeviceId");


CREATE INDEX "IX_AcsTasks_Status" ON "AcsTasks" ("Status");


CREATE INDEX "IX_AcsTasks_Status_NextAttemptAt" ON "AcsTasks" ("Status", "NextAttemptAt");


