using ISUPSDK.AccessControl.WebApi.Structure;
using System;
using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.SdkService.AlarmService
{
    internal partial class HCISUPAlarm
    {
        public const int NET_EHOME_DEVICEID_LEN = 256; //设备ID长度
        public const int MAX_DEVICE_ID_LEN = 256;
        public const int NET_EHOME_SERIAL_LEN = 12;

        public const int MAX_TIME_LEN = 32;        //时间字符串长度
        public const int MAX_REMARK_LEN = 64;           //报警备注长度
        public const int MAX_URL_LEN = 512;        //URL长度
        public const int MAX_UUID_LEN = 64;
        public const int CID_DES_LEN_EX = 256;
        public const int CID_DES_LEN = 32;              //CID报警描述长度
        public const int MAX_ALARM_STRUCT_LEN = 2048;   //报警结构体最大长度
        public const int MAX_ALARM_ANSWER_LEN = 512;   //报警相应xml长度
        public const int MAX_ALARM_DESCRIPTION_LEN = 64;//报警响应描述最大长度
        public const int MAX_ALARM_XML_LEN = 2048;      //最大xml报警长度
        public const int NAME_LEN = 32;

        public const int MAX_SERIAL_LEN = 12;
        public const int EHOME_STATUS_200 = 200;
        public const int EHOME_STATUS_400 = 400;
        public const int EHOME_STATUS_401 = 401;

        //报警
        public const int EHOME_ALARM_UNKNOWN = 0;  //未知报警类型
        public const int EHOME_ALARM = 1;        //Ehome基本报警
        public const int EHOME_ALARM_HEATMAP_REPORT = 2;        //热度图报告
        public const int EHOME_ALARM_FACESNAP_REPORT = 3;       //人脸抓拍报告
        public const int EHOME_ALARM_GPS = 4;        //GPS信息上传
        public const int EHOME_ALARM_CID_REPORT = 5;        //报警主机CID告警上传
        public const int EHOME_ALARM_NOTICE_PICURL = 6;        //图片URL上报
        public const int EHOME_ALARM_NOTIFY_FAIL = 7;        //异步失败通知
        public const int EHOME_ALARM_SELFDEFINE = 9;      //自定义报警上传
        public const int EHOME_ALARM_ACS = 11;        //门禁事件上报
        public const int EHOME_ISAPI_ALARM = 13;
        public const int EHOME_ALARM_FACETEMP = 21;    //Only Temperature Mode

        public enum EN_ALARM_TYPE
        {
            ALARM_TYPE_DISK_FULL = 0,                     //0-硬盘满
            ALARM_TYPE_DISK_WRERROR,                     //1-读写硬盘出错
            ALARM_TYPE_VIDEO_LOST = 5,                   //5-视频(信号)丢失*/
            ALARM_TYPE_EXTERNAL,                         //6-外部(信号量)报警
            ALARM_TYPE_VIDEO_COVERED,                    //7-视频遮盖
            ALARM_TYPE_MOTION,                           //8-移动侦测
            ALARM_TYPE_STANDARD_NOTMATCH,                //9-制式不匹配
            ALARM_TYPE_SPEEDLIMIT_EXCEED,                //10-超速
            ALARM_TYPE_PIR,                                 //11-PIR报警
            ALARM_TYPE_WIRELESS,                         //12-无线报警
            ALARM_TYPE_CALL_HELP,                         //13-呼救报警
            ALARM_TYPE_DISARM,                             //14-布撤防报警
            ALARM_TYPE_STREAM_PRIVATE,                     //15-码流隐私状态改变通知
            ALARM_TYPE_PIC_UPLOAD_FAIL,                     //16-设备上传图片失败
            ALARM_TYPE_LOCAL_REC_EXCEPTION,                 //17-设备本地录像（抓图）异常
            ALARM_TYPE_UPGRADE_FAIL,                     //18-设备版本升级失败
            ALARM_TYPE_ILLEGAL_ACCESS,                     //19-非法访问
            ALARM_TYPE_SOUNDLIMIT_EXCEED = 80,           //80-声音分贝数超标
            ALARM_TYPE_TRIFFIC_VIOLATION = 90,             //90-违章报警
            ALARM_TYPE_ALARM_CONTROL,                     //91-布控报警
            ALARM_TYPE_FACE_DETECTION = 97,                 //97-人脸侦测
            ALARM_TYPE_DEFOUSE_DETECTION,                 //98-虚焦侦测
            ALARM_TYPE_AUDIO_EXCEPTION,                     //99-音频异常侦测
            ALARM_TYPE_SCENE_CHANGE,                     //100-场景变更报警
            ALARM_TYPE_TRAVERSE_PLANE,                     //101-穿越警戒面（越界侦测）
            ALARM_TYPE_ENTER_AREA,                         //102-目标进入区域
            ALARM_TYPE_LEAVE_AREA,                         //103-目标离开区域
            ALARM_TYPE_INTRUSION,                         //104-周界入侵（区域入侵）
            ALARM_TYPE_LOITER,                             //105-徘徊
            ALARM_TYPE_LEFT_TAKE,                         //106-物品遗留拿取
            ALARM_TYPE_CAR_STOP,                         //107-停车
            ALARM_TYPE_MOVE_FAST,                         //108-快速运动
            ALARM_TYPE_HIGH_DENSITY,                     //109-人员聚集
            ALARM_TYPE_PDC_BY_TIME,                         //110-人流量按时间段统计上报
            ALARM_TYPE_PDC_BY_FRAME,                     //111-人流量单帧统计上报
            ALARM_TYPE_LEFT,                             //112-物品遗留
            ALARM_TYPE_TAKE,                             //113-物品拿取
            ALARM_TYPE_ENV_LIMIT = 8800,                 //8800-动环环境量超限报警
            ALARM_TYPE_ENV_REAL_TIME,                     //8801-动环环境量实时数据上报
            ALARM_TYPE_ENV_EXCEPTION,                     //8802-动环异常上报
            ALARM_TYPE_HIGH_TEMP = 40961,                 //40961-设备温度过高报警
            ALARM_TYPE_ACC_EXCEPTION                     //40962-设备加速度异常报警
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_ALARM_INFO
        {
            public UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_TIME_LEN)]
            public byte[] szAlarmTime;   //报警触发时间，时间格式,YYYY-MM-DD HH:MM:SS
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_DEVICE_ID_LEN)]
            public byte[] szDeviceID;//设备注册ID
            public UInt32 dwAlarmType;                  //报警类型见EN_ALARM_TYPE枚举变量
            public UInt32 dwAlarmAction;                //报警动作0:开始    1:停止
            public UInt32 dwVideoChannel;               //各报警中的意义见表4.2.2.2-1
            public UInt32 dwAlarmInChannel;             //各报警中的意义见表4.2.2.2-1
            public UInt32 dwDiskNumber;                 //各报警中的意义见表4.2.2.2-1
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_REMARK_LEN)]
            public byte[] byRemark;      //各报警中的意义见表4.2.2.2-1
            public byte byRetransFlag;                  //重传标记，0-实时包，1-重传包
            public byte byTimeType;    //时间类型，0-设备本地时间，1-UTC时间
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byRes1;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_TIME_LEN)]
            public byte[] szAlarmUploadTime;    //报警上传时间，时间格式,YYYY-MM-DD HH:MM:SS
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 28)]
            public byte[] byRes;
            public void Init()
            {
                szAlarmTime = new byte[MAX_TIME_LEN];
                szDeviceID = new byte[MAX_DEVICE_ID_LEN];
                szAlarmUploadTime = new byte[MAX_TIME_LEN];
                byRemark = new byte[MAX_REMARK_LEN];
                byRes1 = new byte[2];
                byRes = new byte[28];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_GPS_INFO
        {
            public UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_TIME_LEN)]
            public byte[] bySampleTime;      //GPS采样时间，格式：YYYY-MM-DD HH:MM:SS
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_DEVICE_ID_LEN)]
            public byte[] byDeviceID;   //设备注册ID
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byDivision;                   //division[0]:'E'or'W'(东经/西经), division[1]:'N'or'S'(北纬/南纬)
            public byte bySatelites;                     //卫星数量
            public byte byPrecision;                     //精度因子，原始值*100
            public UInt32 dwLongitude;                     //经度，取值范围为（0～180*3600*100），转换公式为：longitude= 实际度*3600*100+实际分*60*100+实际秒*100
            public UInt32 dwLatitude;                      //纬度，取值范围为（0～90*3600*100），转换公式为：latitude = 实际度*3600*100+实际分*60*100+实际秒*100
            public UInt32 dwDirection;                     //方向，取值范围为（0～359.9*100），正北方向为0，转换公式为：direction= 实际方向*100
            public UInt32 dwSpeed;                         //速度，取值范围为（0～999.9*100000），转换公式为：speed =实际速度*100000，相当于cm/h
            public UInt32 dwHeight;                         //高度，单位：cm
            public byte byRetransFlag;                     //重传标记，0-实时包，1-重传包
            public byte byLocateMode;                      //定位模式(初值0)，仅NMEA0183 3.00版本输出，值：0-自主定位，1- 差分，2- 估算，3- 数据无效 
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byRes1;
            public UInt32 dwMileage;                       //设备里程数统计，单位为米，整型，每天第一次开机或者00:00:00时清零，后续累加当天里程数上报到中心 
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 56)]
            public byte[] byRes;
            public void Init()
            {
                bySampleTime = new byte[MAX_TIME_LEN];
                byDeviceID = new byte[MAX_DEVICE_ID_LEN];
                byDivision = new byte[2];
                byRes1 = new byte[2];
                byRes = new byte[56];
            }

        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_HEATMAP_VALUE
        {
            public UInt32 dwMaxHeatMapValue;    //最大热度值
            public UInt32 dwMinHeatMapValue;    //最小热度值
            public UInt32 dwTimeHeatMapValue;    //平均热度值
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_PIXEL_ARRAY_SIZE
        {
            public UInt32 dwLineValue;    //像素点行值
            public UInt32 dwColumnValue;//像素点列值
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_HEATMAP_REPORT
        {
            public UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_DEVICE_ID_LEN)]
            public char[] byDeviceID;   //设备注册ID
            public UInt32 dwVideoChannel;                     //通道号
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_TIME_LEN)]
            public char[] byStartTime;       //开始时间，格式：YYYY-MM-DD HH:MM:SS
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_TIME_LEN)]
            public char[] byStopTime;        //结束时间，格式：YYYY-MM-DD HH:MM:SS
            public NET_EHOME_HEATMAP_VALUE struHeatmapValue;//热度值
            public NET_EHOME_PIXEL_ARRAY_SIZE struPixelArraySize;    //热度图大小
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_URL_LEN)]
            public char byPixelArrayData;     //热度图数据索引
            public byte byRetransFlag;                     //重传标记，0-实时包，1-重传包
            public byte byTimeType;    //时间类型，0-设备本地时间，1-UTC时间
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 62)]
            public byte byRes;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_HUMAN_FEATURE
        {
            public byte byAgeGroup;         //年龄属性，1-婴幼儿，2-儿童，3-少年，4-青少年，5-青年，6-壮年，7-中年，8-中老年，9-老年
            public byte bySex;             //性别属性，1-男，2-女
            public byte byEyeGlass;       //是否戴眼睛，1-不戴，2-戴
            public byte byRes;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_ZONE
        {
            public UInt32 dwX;        //X轴坐标
            public UInt32 dwY;        //Y轴坐标
            public UInt32 dwWidth;  //宽度
            public UInt32 dwHeight;    //高度
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_FACESNAP_REPORT
        {
            public UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_DEVICE_ID_LEN)]
            public byte[] byDeviceID;   //设备注册ID
            public UInt32 dwVideoChannel;                     //通道号
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_TIME_LEN)]
            public byte[] byAlarmTime;       //报警时间，格式：YYYY-MM-DD HH:MM:SS
            public UInt32 dwFacePicID;                     //人脸图ID
            public UInt32 dwFaceScore;                     //人脸评分，0-100
            public UInt32 dwTargetID;                         //目标ID
            public NET_EHOME_ZONE struTarketZone;             //目标区域，归一化坐标：数值为当前画面的百分比大小*1000，精度为小数点后三位
            public NET_EHOME_ZONE struFacePicZone;         //人脸子图区域，归一化坐标：数值为当前画面的百分比大小*1000，精度为小数点后三位
            public NET_EHOME_HUMAN_FEATURE struHumanFeature;//人属性
            public UInt32 dwStayDuration;                   //停留画面中时间
            public UInt32 dwFacePicLen;                     //人脸图长度，单位：字节
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_URL_LEN)]
            public byte[] byFacePicUrl;          //人脸子图数据索引
            public UInt32 dwBackgroudPicLen;                     //背景图片长度，单位：字节
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_URL_LEN)]
            public byte[] byBackgroudPicUrl;         //背景图片数据索引
            public byte byRetransFlag;                     //重传标记，0-实时包，1-重传包
            public byte byTimeType;    //时间类型，0-设备本地时间，1-UTC时间
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 62)]
            public byte[] byRes;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_CID_PARAM
        {
            public UInt32 dwUserType;    //用户类型，1键盘用户 2网络用户，其他值表示无效
            public Int32 lUserNo;        //用户类型，-1表示无效
            public Int32 lZoneNo;        //防区号，-1表示无效
            public Int32 lKeyboardNo;    //键盘号，
            public Int32 lVideoChanNo;    //视频通道号
            public Int32 lDiskNo;        //硬盘号
            public Int32 lModuleAddr;    //模块地址
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = NAME_LEN)]
            public string byUserName;    //用户名
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] byRes;
            public void Init()
            {
                byRes = new byte[32];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_CID_INFO_INTERNAL_EX
        {
            public byte byRecheck;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] byRes;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_UUID_LEN)]
            public string byUUID;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_URL_LEN)]
            public string byVideoURL;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_URL_LEN)]
            public string byCIDDescribeEx;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] byRes1;
            public void Init()
            {
                byRes = new byte[3];
                byRes1 = new byte[256];
            }
        }


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_CID_INFO
        {
            public UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_DEVICE_ID_LEN)]
            public string byDeviceID;   //设备注册ID
            public UInt32 dwCIDCode;                         //CID报告代码
            public UInt32 dwCIDType;                      //CID报警类型
            public UInt32 dwSubSysNo;                         //产生报告的子系统号，0为全局报告，子系统范围0~32
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = CID_DES_LEN)]
            public string byCIDDescribe;                    //CID报警描述
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_TIME_LEN)]
            public string byTriggerTime;     //CID报警发生时间，格式：YYYY-MM-DD HH:MM:SS
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_TIME_LEN)]
            public string byUploadTime;         //CID报告上传时间，格式：YYYY-MM-DD HH:MM:SS
            public NET_EHOME_CID_PARAM struCIDParam;         //CID报警参数
            public byte byTimeDiffH;
            public byte byTimeDiffM;
            public byte byExtend;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 5)]
            public byte[] byRes1;
            public IntPtr pCidInfoEx;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 52)]
            public byte[] byRes;
            public void Init()
            {
                struCIDParam.Init();
                byRes1 = new byte[5];
                byRes = new byte[52];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_NOTICE_PICURL
        {
            public UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_DEVICE_ID_LEN)]
            public char[] byDeviceID;   //设备注册ID
            public UInt16 wPicType;    //图片业务类型，0-PU定时抓图上传中心，1-PU报警抓图上传中心(AlarmType、AlarmChan、AlarmTime 三个节点在PicType为时才有效) ，2-CU手动抓图上传中心，
            //3-CU下载图片，4-外部触发PU抓图上传中心（比如DVR通过串口接收到POS机刷卡触发抓图请求）
            public UInt16 wAlarmType; //报警类型，6-报警输入，7-视频遮挡，8-移动侦测，11-PIR报警，12-门磁报警，13-呼救报警    
            public UInt32 dwAlarmChan;            //报警通道编号
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_TIME_LEN)]
            public char[] byAlarmTime;     //报警产生时间，格式：YYYY-MM-DD HH:MM:SS
            public UInt32 dwCaptureChan;            //图片抓拍通道编号
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_TIME_LEN)]
            public char[] byPicTime;                //图片时间，格式：YYYY-MM-DD HH:MM:SS
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_URL_LEN)]
            public char[] byPicUrl;         //图片数据索引
            public UInt32 dwManualSnapSeq;    // 请求流水号（字节整数），PicType值为2、3时有效
            public byte byRetransFlag;                     //重传标记，0-实时包，-重传包
            public byte byTimeType;    //时间类型，0-设备本地时间，1-UTC时间
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 30)]
            public byte[] byRes;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_NOTIFY_FAIL_INFO
        {
            private UInt32 dwSize;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_DEVICE_ID_LEN)]
            private char byDeviceID;   //设备注册ID
            private UInt16 wFailedCommand;    //失败的命令，1-抓图失败
            private UInt16 wPicType;

            //图片业务类型，失败命令为抓图失败时有效，0-PU定时抓图上传中心，1-PU报警抓图上传中心，2-CU手动抓图上传中心，
            //3-CU下载图片，4-外部触发PU抓图上传中心    
            private UInt32 dwManualSnapSeq;    // 请求流水号（字节整数），PicType值为2、3时有效
            private byte byRetransFlag;                     //重传标记，0-实时包，1-重传包
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 31)]
            private byte[] byRes;
        }


        public delegate bool EHomeMsgCallBack(Int32 lHandle, IntPtr pAlarmMsg/*ref NET_EHOME_ALARM_MSG pAlarmMsg*/, IntPtr pUser);

        //        public delegate bool AlarmMsgCallBack(long lHandle, IntPtr pAlarmMsg, IntPtr pUser);

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_ALARM_LISTEN_PARAM
        {
            public HCEHomePublic.NET_EHOME_IPADDRESS struAddress;  //本地监听信息，IP为0.0.0.0的情况下，默认为本地地址，多个网卡的情况下，默认为从操作系统获取到的第一个
            public EHomeMsgCallBack fnMsgCb; //报警信息回调函数
            public IntPtr pUserData;   //用户数据
            public byte byProtocolType;  //协议类型，0-TCP(保留，暂不支持),1-UDP,, 2-MQTT
            public byte byUseCmsPort; //是否复用CMS端口,0-不复用，非0-复用，如果复用cms端口，协议类型字段无效（此时本地监听信息struAddress填本地回环地址）
            public byte byUseThreadPool;  //0-回调报警时，使用线程池，1-回调报警时，不使用线程池，默认情况下，报警回调的时候，使用线程池
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 29)]
            public byte[] byRes;
            public void Init()
            {
                struAddress.Init();
                byRes = new byte[29];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_ALARM_ISAPI_INFO
        {
            public IntPtr pAlarmData;         // 报警数据
            public Int32 dwAlarmDataLen;     // 报警数据长度
            public byte byDataType;         // 0-invalid,1-xml,2-json
            public byte byPicturesNumber;   // 图片数量
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byRes;
            public IntPtr pPicPackData;       // 图片变长部分,byPicturesNumber个NET_EHOME_ALARM_ISAPI_PICDATA
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] byRes1;

            public void Init()
            {
                pAlarmData = IntPtr.Zero;
                dwAlarmDataLen = 0;
                byDataType = 0;
                byPicturesNumber = 0;
                byRes1 = new byte[2];
                pPicPackData = IntPtr.Zero;
                byRes1 = new byte[32];
            }
        }


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_ALARM_MSG
        {
            public uint dwAlarmType;      //报警类型，见EN_ALARM_TYPE
            public IntPtr pAlarmInfo;       //报警内容（结构体）
            public uint dwAlarmInfoLen;   //结构体报警内容长度
            public IntPtr pXmlBuf;          //报警内容（XML）
            public uint dwXmlBufLen;      //xml报警内容长度
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = NET_EHOME_SERIAL_LEN)]
            public byte[] sSerialNumber; //设备序列号，用于进行Token认证
            public IntPtr pHttpUrl;
            public int dwHttpUrlLen;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] byRes;

            public void Init()
            {
                dwAlarmType = 0;
                pAlarmInfo = IntPtr.Zero;
                dwAlarmInfoLen = 0;
                pXmlBuf = IntPtr.Zero;
                dwXmlBufLen = 0;
                sSerialNumber = new byte[NET_EHOME_SERIAL_LEN];
                byRes = new byte[12];
            }
        }
        public enum AlarmType
        {
            EHOME_ALARM_UNKNOWN = 0,         //未知报警类型
            EHOME_ALARM = 1,                   //Ehome基本报警
            EHOME_ALARM_HEATMAP_REPORT = 2,    //热度图报告
            EHOME_ALARM_FACESNAP_REPORT = 3,   //人脸抓拍报告
            EHOME_ALARM_GPS = 4,                //GPS信息上传
            EHOME_ALARM_CID_REPORT = 5,           //报警主机CID告警上传
            EHOME_ALARM_NOTICE_PICURL = 6,         //图片URL上报
            EHOME_ALARM_NOTIFY_FAIL = 7,           //异步失败通知
            EHOME_ALARM_SELFDEFINE = 9,            //自定义报警上传
            EHOME_ALARM_DEVICE_NETSWITCH_REPORT = 10,    //设备网络切换上传
            EHOME_ALARM_ACS = 11,                      //门禁事件上报
            EHOME_ALARM_WIRELESS_INFO = 12,        //无线网络信息上传
            EHOME_ISAPI_ALARM = 13,                //ISAPI报警上传
            EHOME_INFO_RELEASE_PRIVATE = 14,       //为了兼容信息发布产品的私有EHome协议报警（不再维护）
            EHOME_ALARM_MPDCDATA = 15,            //车载设备的客流数据
        }
        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_ALARM_LISTEN_PARAM_LOCAL
        {
            private NET_EHOME_ALARM_LISTEN_PARAM struAlaramListenParam;
            private byte byAccessSecurity;       //0-兼容模式（允许任意版本的协议接入），1-普通模式（只支持4.0以下版本，不支持协议安全的版本接入） 2-安全模式（只允许4.0以上版本，支持协议安全的版本接入）
        }

        public struct NET_EHOME_AMS_ADDRESS
        {
            public int dwSize;
            public byte byEnable;  //0-关闭CMS接收报警功能，1-开启CMS接收报警功能
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] byRes1;
            public HCEHomePublic.NET_EHOME_IPADDRESS struAddress;   //AMS本地回环地址
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] byRes2;
            public void Init()
            {
                dwSize = 0;
                byEnable = 0;
                byRes1 = new byte[3];
                struAddress.Init();
            }
        }
        //public delegate bool EHomeMsgCallBack(int iHandle, ref NET_EHOME_ALARM_MSG pAlarmMsg, IntPtr pUser);

        #region HCISUPAlarm.dll function definition

        //SDK Init
        [DllImport("HCISUPAlarm.dll")]
        public static extern bool NET_EALARM_Init();

        //SDK Fni 
        [DllImport(@"HCISUPAlarm.dll")]
        public static extern bool NET_EALARM_Fini();

        [DllImport(@"HCISUPAlarm.dll")]
        public static extern int NET_EALARM_GetLastError();

        [DllImport(@"HCISUPAlarm.dll")]
        public static extern bool NET_EALARM_SetLogToFile(Int32 iLogLevel, String strLogDir, bool bAutoDel);

        //[DllImport(@"HCISUPAlarm.dll")]
        //public static extern bool NET_EALARM_StopListen(ref NET_EHOME_ALARM_LISTEN_PARAM pAlarmListenParam);

        [DllImport(@"HCISUPAlarm.dll")]
        public static extern int NET_EALARM_GetBuildVersion();

        [DllImport(@"HCISUPAlarm.dll")]
        public static extern int NET_EALARM_StartListen(ref NET_EHOME_ALARM_LISTEN_PARAM pAlarmListenParam);
        //Close Cms Alarm
        [DllImport(@"HCISUPAlarm.dll")]
        public static extern bool NET_EALARM_StopListen(int iListenHandle);

        [DllImport(@"HCISUPAlarm.dll")]
        public static extern bool NET_EALARM_SetSDKLocalCfg(HCEHomePublic.NET_EHOME_LOCAL_CFG_TYPE enumType, IntPtr lpInBuff);

        [DllImport(@"HCISUPAlarm.dll")]
        public static extern bool NET_EALARM_SetDeviceSessionKey(ref HCEHomePublic.NET_EHOME_DEV_SESSIONKEY pDeviceKey);
        #endregion
    }
}
