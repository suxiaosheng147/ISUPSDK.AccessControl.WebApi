/* ============================================================================
   异步下发任务表 AcsTasks 建表脚本（SQL Server）

   用途：服务启动时会自动 EnsureCreated 建库建表（无 AutoCreate 开关）；
         本脚本供 DBA 预先建表、或核对/重建表结构使用。
   生成：由 EF Core 的 Database.GenerateCreateScript() 输出，与实体模型严格一致；
         修改 AcsTask / AcsTaskDbContext 后需重新生成。
   执行：在目标库中执行本脚本（先 USE 目标库 / 选择正确的默认 schema）。

   注意：老库升级时服务启动会自动补 PayloadFormat 列（SQL Server 写法为
         ALTER TABLE AcsTasks ADD PayloadFormat ...，不带 COLUMN 关键字），无需人工处理。
   ============================================================================ */
CREATE TABLE [AcsTasks] (
    [Id] nvarchar(36) NOT NULL,
    [DeviceId] nvarchar(128) NOT NULL,
    [Entity] int NOT NULL,
    [Operation] int NOT NULL,
    [Status] int NOT NULL,
    [Payload] nvarchar(max) NULL,
    [PayloadFormat] nvarchar(16) NOT NULL,
    [BinaryPath] nvarchar(max) NULL,
    [TimeoutMs] bigint NOT NULL,
    [Attempts] int NOT NULL,
    [MaxAttempts] int NOT NULL,
    [DeviceSuccess] bit NOT NULL,
    [DeviceStatusCode] int NOT NULL,
    [SubStatusCode] nvarchar(128) NULL,
    [ResultJson] nvarchar(max) NULL,
    [Error] nvarchar(2048) NULL,
    [CreatedAt] bigint NOT NULL,
    [StartedAt] bigint NULL,
    [CompletedAt] bigint NULL,
    [NextAttemptAt] bigint NOT NULL,
    [Description] nvarchar(512) NULL,
    CONSTRAINT [PK_AcsTasks] PRIMARY KEY ([Id])
);
GO


CREATE INDEX [IX_AcsTasks_CreatedAt] ON [AcsTasks] ([CreatedAt]);
GO


CREATE INDEX [IX_AcsTasks_DeviceId] ON [AcsTasks] ([DeviceId]);
GO


CREATE INDEX [IX_AcsTasks_Status] ON [AcsTasks] ([Status]);
GO


CREATE INDEX [IX_AcsTasks_Status_NextAttemptAt] ON [AcsTasks] ([Status], [NextAttemptAt]);
GO


