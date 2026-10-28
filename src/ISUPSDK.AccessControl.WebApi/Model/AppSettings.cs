
using System.Diagnostics.SymbolStore;

namespace ISUPSDK.AccessControl.WebApi.Model
{
    /// <summary>
    /// 基础配置
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// 本机IP地址
        /// </summary>
        public string ServerAddress { get; set; }

        /// <summary>
        /// 本机IP地址
        /// </summary>
        public string ServerAddressOutIP { get; set; }


        /// <summary>
        /// 本机公网IP地址
        /// </summary>
        public string ServerAddressOut { get; set; }

        /// <summary>
        /// 图片储存地址，完整路径,不能存在中文
        /// </summary>
        public string PicturePath { get; set; }


        /// <summary>
        /// 报警存储图片位置，特别版本
        /// </summary>
        public string Photo { get; set; }


        /// <summary>
        /// 报警存储图片位置，特别版本
        /// </summary>
        public string Photo1 { get; set; }

        /// <summary>
        /// 注册服务器UDP端口（如果设备配置的是ISUP 5.0协议版本，那使用的是TCP协议）
        /// </summary>
        public int InitPort { get; set; }

        /// <summary>
        /// 每次下发获取记录数
        /// </summary>
        public int RecordNumber { get; set; }

        /// <summary>
        /// 线程休眠时间
        /// </summary>
        public int SleepTime { get; set; }

        /// <summary>
        /// ISPU 5.0 登录密钥  默认Krbb2023  （如果更改，全部的设备设置都要统一）
        /// </summary>
        public string LogOnSecretKey { get; set; }

        /// <summary>
        /// 是否下载pic（门禁中是否提前下载图片，保存到本地）
        /// </summary>
        public bool IsDownloadPic { get; set; } = false;


        /// <summary>
        /// 获取门禁信息
        /// </summary>
        public string DoorUpdateDataUrl { get; set; }


        /// <summary>
        /// 获取待更信息
        /// </summary>
        public string UpdateInfoUrl { get; set; }

        /// <summary>
        /// 学生待更信息
        /// </summary>
        public string StudentUpdateDataUrl { get; set; }


        /// <summary>
        /// 亲属待更信息
        /// </summary>
        public string ParentUpdateDataUrl { get; set; }

        /// <summary>
        /// 职工待更信息
        /// </summary>
        public string StaffUpdateDataUrl { get; set; }

        /// <summary>
        /// 初始化待更信息
        /// </summary>
        public string ReturnInitUrl { get; set; }

        /// <summary>
        /// 返回下发成功信息给平台接口
        /// </summary>
        public string ReturnDataUrl { get; set; }

        /// <summary>
        /// 返回下发失败信息给平台接口
        /// </summary>
        public string ReturnErrorUrl { get; set; }

        /// <summary>
        /// 获取机器ID
        /// </summary>
        public string GetMachineIdUrl { get; set; }

        /// <summary>
        /// 更新HKLoginId
        /// </summary>
        public string UpdateHKLoginIdUrl { get; set; }

        /// <summary>
        /// 消息队列地址
        /// </summary>
        public string MessageQueueAddress { get; set; }

        /// <summary>
        /// 消息队列地址
        /// </summary>
        public string MessageQueueAddress_MachineLeave { get; set; }

        /// <summary>
        /// 是否开启消息队列
        /// </summary>
        public bool IsMessageQueue { get; set; }


        /// <summary>
        /// 监听事件url
        /// </summary>
        public string MonitorEventUrl { get; set; }


        /// <summary>
        /// 获取请假同步数据
        /// </summary>
        public string LeaveSyncDatas { get; set; }

        /// <summary>
        /// 获取请假恢复同步数据
        /// </summary>
        public string LeaveResetSyncDatas { get; set; }

        /// <summary>
        /// 返回请假同步成功数据
        /// </summary>
        public string UpdateLeave { get; set; }

        /// <summary>
        /// 返回请假恢复同步成功数据
        /// </summary>
        public string UpdateReset { get; set; }

        /// <summary>
        /// WebCheckCode
        /// </summary>
        public string? WebCheckCode { get; set; }


        /// <summary>
        /// WebCheckCode 解密出来的
        /// </summary>
        public string WebCheckCode1 { get; set; }

        /// <summary>
        /// 人脸图片类型（下发人脸）  1=  ， 2 =需要图片下载到本地通过iis 来访问图片进行机器下发人脸 ;3= 
        /// </summary>
        public int FacePicType { get; set; }


        /// <summary>
        /// 替换域名  比如 http://121.9.250.34/
        /// </summary>
        public string ReplaceDomainName { get; set; }

        /// <summary>
        /// 环境
        /// </summary>
        public string Environment { get; set; }



        /// <summary>


        /// <summary>
        /// NtpServer  
        /// </summary>
        public string NtpServer { get; set; } = "";



        /// <summary>
        /// 同步门禁数据（认证规则）
        /// </summary>
        public string SyncDoorData { get; set; }

        /// <summary>
        /// 同步人员数据（认证规则）
        /// </summary>
        public string SyncPersonData { get; set; }

        /// <summary>
        /// 修改信息数据
        /// </summary>
        public string PutInfoData { get; set; }

        /// <summary>
        /// 是否认证规则绑定机器
        /// </summary>
        public string IsAuthRuleBindMachine { get; set; }


        /// <summary>
        /// 获取请假同步数据（认证规则）
        /// </summary>
        public string AuthRuleLeaveSyncDatasUrl { get; set; }

        /// <summary>
        /// 获取请假恢复同步数据（认证规则）
        /// </summary>
        public string AuthRuleLeaveResetSyncDatasUrl { get; set; }


        /// <summary>
        /// 更新请假
        /// </summary>
        public string AuthRuleUpdateLeaveUrl { get; set; }

        /// <summary>
        /// 最大总线程休眠时间
        /// </summary>
        public int MaxTotalSleepTime { get; set; }

        /// <summary>
        /// 最小总线程休眠时间
        /// </summary>
        public int MinTotalSleepTime { get; set; }

      
        /// <summary>
        /// api线程休眠时间
        /// </summary>
        public int ApiSleepTime { get; set; }


        /// <summary>
        /// 
        /// </summary>
        public string SyncAcsEventUrl        { get; set; }



        /// <summary>
        /// 
        /// </summary>
        public int PushAccessType { get; set; } = 0;

        /// <summary>
        /// 
        /// </summary>
        public string MonitorEventURL { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool ForceUpdatePic { get; set; } = true;


        public NightBoostSettings NightBoost { get; set; } = new();

        /// <summary>
        /// 人员下发队列数据库（默认 SQLite 的 isup_person_queue.db）。
        /// </summary>
        public ISUPSDK.AccessControl.WebApi.Data.PersonOperationDatabaseSettings? Database { get; set; }

        /// <summary>
        /// 人员下发队列（人员增删改先入库，再由定时任务通过控制器接口下发设备）。
        /// </summary>
        public ISUPSDK.AccessControl.WebApi.Data.PersonOperationQueueSettings? PersonOperationQueue { get; set; }

    }

    public class NightBoostSettings
    {
        public bool Enabled { get; set; }
        public List<TimeRange> TimeRanges { get; set; } = new();
    }

    public class TimeRange
    {
        public int StartHour { get; set; }
        public int EndHour { get; set; }
        public double MinMultiplier { get; set; } = 1.0;
        public double MaxMultiplier { get; set; } = 1.0;
        public int ExtraMilliseconds { get; set; }
    }
}
