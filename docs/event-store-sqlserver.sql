/* ============================================================================
   门禁事件表 AcsEvents 建表脚本（SQL Server）

   用途：当 ISUP:EventStore:AutoCreate=false 时，由运维手工建表。
   生成：由 EF Core 的 Database.GenerateCreateScript() 输出，与实体模型严格一致；
         修改 AcsEventRecord / AcsEventDbContext 后需重新生成。
   执行：在目标库中执行本脚本（先 USE 目标库 / 选择正确的默认 schema）。

   注意：UX_AcsEvents_DedupHash 唯一索引是"重复事件只落一行"的关键，不要省略。
   ============================================================================ */
CREATE TABLE [AcsEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [DedupKey] nvarchar(200) NOT NULL,
    [DedupHash] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(128) NOT NULL,
    [Source] nvarchar(32) NOT NULL,
    [SerialNumber] nvarchar(128) NULL,
    [EventSerialNo] nvarchar(64) NULL,
    [EventType] nvarchar(128) NULL,
    [SubEventType] nvarchar(64) NULL,
    [Major] int NULL,
    [Minor] int NULL,
    [EmployeeNo] nvarchar(64) NULL,
    [CardNo] nvarchar(64) NULL,
    [PersonName] nvarchar(128) NULL,
    [DoorNo] int NULL,
    [VerifyMode] nvarchar(64) NULL,
    [AlarmTopic] nvarchar(256) NULL,
    [AlarmType] int NOT NULL,
    [DataType] int NOT NULL,
    [PayloadFormat] nvarchar(16) NOT NULL,
    [Payload] nvarchar(max) NULL,
    [PayloadBytes] int NOT NULL,
    [PayloadTruncated] bit NOT NULL,
    [PictureFileNames] nvarchar(max) NULL,
    [IsAccessControl] bit NOT NULL,
    [HttpUrl] nvarchar(512) NULL,
    [ListenHandle] int NULL,
    [EventTime] bigint NOT NULL,
    [ReceivedAt] bigint NOT NULL,
    [EnqueuedAt] bigint NOT NULL,
    [StoredAt] bigint NOT NULL,
    [QueueProvider] nvarchar(32) NULL,
    [QueueMessageId] nvarchar(128) NULL,
    [DeliveryAttempts] int NOT NULL,
    CONSTRAINT [PK_AcsEvents] PRIMARY KEY ([Id])
);
GO


CREATE INDEX [IX_AcsEvents_Acs_EventTime] ON [AcsEvents] ([IsAccessControl], [EventTime]);
GO


CREATE INDEX [IX_AcsEvents_Device_EventTime] ON [AcsEvents] ([DeviceId], [EventTime]);
GO


CREATE INDEX [IX_AcsEvents_EmployeeNo] ON [AcsEvents] ([EmployeeNo]);
GO


CREATE INDEX [IX_AcsEvents_EventTime] ON [AcsEvents] ([EventTime]);
GO


CREATE INDEX [IX_AcsEvents_ReceivedAt] ON [AcsEvents] ([ReceivedAt]);
GO


CREATE UNIQUE INDEX [UX_AcsEvents_DedupHash] ON [AcsEvents] ([DedupHash]);
GO


