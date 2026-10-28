using ISUPSDK.AccessControl.WebApi.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Serialization;

// 类型名 AccessControl 会被上层命名空间 ISUPSDK.AccessControl 遮蔽，这里用别名消歧。
using AccessControlModel = ISUPSDK.AccessControl.WebApi.Model.AccessControl;

namespace ISUPSDK.AccessControl.WebApi
{
    //[XmlRoot(ElementName = "xml")]
    //[Serializable]
    //public class Message
    //{
    //    [XmlElement(ElementName = "PPVSPMessage")]
    //    public Record PPVSPMessage { get; set; }
    //}

    [XmlRoot(ElementName = "PPVSPMessage")]
    [Serializable]
    public class PPVSPMessage
    {
        [XmlElement(ElementName = "Version")]
        public string Version { get; set; }
        [XmlElement(ElementName = "Sequence")]
        public int Sequence { get; set; }
        [XmlElement(ElementName = "CommandType")]
        public string CommandType { get; set; }
        public string Command { get; set; }
        [XmlElement(ElementName = "Params")]
        public Params Params { get; set; }
    }

    [XmlRoot(ElementName = "Params")]
    [Serializable]
    public class Params
    {
        [XmlElement(ElementName = "DeviceID")]
        public string DeviceID { get; set; }
        [XmlElement(ElementName = "Time")]
        public string Time { get; set; }
        public DateTime TrueTime { get { return Convert.ToDateTime(Time); } }
        [XmlElement(ElementName = "CardType")]
        public int CardType { get; set; }
        [XmlElement(ElementName = "CardReaderNo")]
        public int CardReaderNo { get; set; }
        [XmlElement(ElementName = "DoorNo")]
        public int DoorNo { get; set; }
        [XmlElement(ElementName = "serialNo")]
        public int serialNo { get; set; }
        [XmlElement(ElementName = "userType")]
        public string userType { get; set; }
        [XmlElement(ElementName = "employeeNoString")]
        public string employeeNoString { get; set; }
        [XmlElement(ElementName = "PicDataUrl")]
        public string PicDataUrl { get; set; }
        [XmlElement(ElementName = "MajorType")]
        public string MajorType { get; set; }
        [XmlElement(ElementName = "MinorType")]
        public string MinorType { get; set; }
        [XmlElement(ElementName = "CardNo")]
        public string cardNo { get; set; }
        [XmlElement(ElementName = "currTemperature")]
        public float currTemperature { get; set; }
    }
    public class im_HKTempLoginMachine
    {
        public int Id { get; set; }

        public int LoginId { get; set; }

        public int CompanyID { get; set; }
        public string MachineSN { get; set; }

        public DateTime AddTime { get; set; }

        public int MId { get; set; }

        public int iUserID { get; set; }
    }
#pragma warning disable IDE1006 // 命名样式
    public class im_HKMachineIsLogout
#pragma warning restore IDE1006 // 命名样式
    {
        public int Id { get; set; }

        public bool IsNeedLogout { get; set; }
    }

    public class im_machine
    {
        public int Id { get; set; }

        public int? Yeyid { get; set; }

        public int? CompanyID { get; set; }
        /// <summary>
        /// 海康机器是否开启Http监听
        /// </summary>
        public int MachineNum { get; set; }

        public string? Password { get; set; }

        public int? Type { get; set; }

        public string MachineSN { get; set; }

        public string Mobile { get; set; }

        public bool? SmsOn { get; set; }

        public bool DoorOn { get; set; }= false;

        public bool? Baoduji { get; set; }

        public string UserAgent { get; set; }

        public DateTime? LinkTime { get; set; }

        public int Place { get; set; }

        public DateTime? LastReadTime { get; set; }

        public string Ip { get; set; }

        public string Port { get; set; }

        public int lLoginID { get; set; }
        /// <summary>
        /// 是否校验卡号，true：是，false：否
        /// </summary>
        public bool IsReadCard { get; set; }
        /// <summary>
        /// 读卡器（0或null：不限，1:进，2：出）
        /// </summary>
        public int? ReadCardMachine { get; set; }
        /// <summary>
        /// 海康机器登录ID
        /// </summary>
        public int? HKLoginId { get; set; }


        /// <summary>
        /// 访问控制
        /// </summary>
        public AccessControlModel? accessControl { get; set; } = null;
    }

    public class UpdateData<T>
    {
        public Boolean Status { get; set; }
        public int ErrType { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    public class Record
    {
        public string dataId { get; set; }
        public string err { get; set; }
        public DateTime time { get; set; }
    }
    //public class FiveOnline
    //{
    //    /// <summary>
    //    /// 机器Id
    //    /// </summary>
    //    public int Id { get; set; }
    //    /// <summary>
    //    /// 登录ID
    //    /// </summary>
    //    public int LoginId { get; set; }
    //    public int MachineNumber { get; set; }
    //    public DateTime AddTime { get; set; }
    //}

    public class picture_path_uri
    {
        public string FILE_PATH { get; set; }
        public string URI { get; set; }
        public string URI_SHA256 { get; set; }
        public string Remark { get; set; }
        public bool IS_TOKEN { get; set; }
    }

    public class SnapData
    {
        public DateTime InOutTime { get; set; }
        public string Snap { get; set; }
        public string UserID { get; set; }
        public string CardID { get; set; }
        public float Temperature { get; set; }
        public int ReadCardMachine { get; set; }

        public string Url { get; set; }
    }

    public class tv_settings
    {
        public int UpdateTime { get; set; }
        public string UpdatePassword { get; set; }
        /// <summary>
        /// 幼儿数据需更新数量
        /// </summary>
        public int childCount { get; set; }
        /// <summary>
        /// 家长数据需更新数量
        /// </summary>
        public int parentCount { get; set; }
        /// <summary>
        /// 职工数据需更新数量
        /// </summary>
        public int staffCount { get; set; }
        /// <summary>
        /// 需要初始化
        /// </summary>
        public bool needInit { get; set; }


        /// <summary>
        /// 门禁数据需更新数量
        /// </summary>
        public int doorCount { get; set; }

        /// <summary>
        /// 需要同步请假数量
        /// </summary>
        public int LeaveSyncCount { get; set; }

        /// <summary>
        /// 请假需要恢复数量
        /// </summary>
        public int LeaveResetSyncCount { get; set; }
    }


    public class Detail
    {
        /// <summary>
        /// 更新数据ID，用于操作成功返回用
        /// </summary>
        public int dataId { get; set; }
        /// <summary>
        /// 幼儿ID，对应于网上数据，更新数据时使用
        /// </summary>
        public int webId { get; set; }
        /// <summary>
        /// 班级ID
        /// </summary>
        public int classId { get; set; }
        /// <summary>
        /// 姓名
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// 性别，true：男，false：女
        /// </summary>
        public bool sex { get; set; }
        /// <summary>
        /// 卡号
        /// </summary>
        public string cardId { get; set; }
        /// <summary>
        /// 门禁号
        /// </summary>
        public int door { get; set; }
        /// <summary>
        /// 照片路径
        /// </summary>
        public string photoPath { get; set; } = "";
        /// <summary>
        /// 是否需要更新照片，true：是，false：否
        /// </summary>
        public bool updatePhoto { get; set; }
        /// <summary>
        /// 幼儿编号
        /// </summary>
        public string code { get; set; }
        /// <summary>
        /// 操作方法，1是增加，2是修改，3是删除
        /// </summary>
        public int method { get; set; }
        /// <summary>
        /// 幼儿名称
        /// </summary>
        public string childName { get; set; }
        /// <summary>
        /// 关系
        /// </summary>
        public string relationship { get; set; }
        /// <summary>
        /// 旧卡号(修改时读取平台修改前的卡号)
        /// </summary>
        public string oldCardId { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 添加时间
        /// </summary>
        [DisplayName("添加时间")]
        public DateTime AddTime { get; set; }

    }


    public class returnErrorModel
    {
        public Guid Id { get; set; }
        public int DataId { get; set; }
        public string Err { get; set; }
        public int ErrCode { get; set; }

        /// <summary>
        /// 添加时间（平台下发的时间，回传时原样带回）
        /// </summary>
        [DisplayName("添加时间")]
        public DateTime? AddTime { get; set; }
    }

    [XmlRoot(ElementName = "Params")]
    [Serializable]
    public class Card
    {
        public string ConfigCmd { get; set; }
        public string ConfigParam1 { get; set; }
        public string ConfigParam2 { get; set; }
        public string ConfigParam3 { get; set; }
        public string ConfigParam4 { get; set; }
    }


    public class DoorData
    {
        /// <summary>
        /// 更新数据ID，用于操作成功返回用
        /// </summary>
        public int dataId { get; set; }
        /// <summary>
        /// 群组ID（目前定为1：家长，2：职工）
        /// </summary>
        public int groupID { get; set; }
        /// <summary>
        /// 门禁功能是否开启（true：开启，false：关闭）
        /// </summary>
        public bool status { get; set; }
        /// <summary>
        /// 门禁打开时间（单位：秒）
        /// </summary>
        public int openTime { get; set; }
        /// <summary>
        /// 门禁开门时间段1开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime1 { get; set; }
        /// <summary>
        /// 门禁开门时间段1结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime1 { get; set; }
        /// <summary>
        /// 门禁开门时间段2开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime2 { get; set; }
        /// <summary>
        /// 门禁开门时间段2结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime2 { get; set; }
        /// <summary>
        /// 门禁开门时间段3开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime3 { get; set; }
        /// <summary>
        /// 门禁开门时间段3结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime3 { get; set; }
        /// <summary>
        /// 门禁开门时间段4开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime4 { get; set; }
        /// <summary>
        /// 门禁开门时间段4结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime4 { get; set; }
        /// <summary>
        /// 操作方法，1是增加，2是修改，3是删除
        /// </summary>
        public int method { get; set; }

        /// <summary>
        /// 时间段,如有时间段，就是设置周计划
        /// </summary>
        public List<TimePeriod> TimePeriods { get; set; }

        /// <summary>
        /// 假日组数据
        /// </summary>
        public List<HolidayGroupData> HolidayGroupDatas { get; set; }

    }


    public class AuthRuleDoorData
    {

        /// <summary>
        /// Id
        /// </summary>
        [DisplayName("Id")]
        public Guid Id { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public int Code { get; set; }
        
        /// <summary>
        /// 操作方法，1是增加，2是修改，3是删除
        /// </summary>
        public int Method { get; set; }

        /// <summary>
        /// 时间段,如有时间段，就是设置周计划
        /// </summary>
        public List<TimePeriod> TimePeriods { get; set; }

        /// <summary>
        /// 假日组数据
        /// </summary>
        public List<HolidayGroupData> HolidayGroupDatas { get; set; }

    }

    /// <summary>
    /// 周计划时间点
    /// </summary>
    public class TimePeriod
    {

        /// <summary>
        /// 表示周几
        /// </summary>
        public int Week { get; set; }


        /// <summary>
        /// 门禁开门时间段1开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime1 { get; set; }
        /// <summary>
        /// 门禁开门时间段1结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime1 { get; set; }
        /// <summary>
        /// 门禁开门时间段2开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime2 { get; set; }
        /// <summary>
        /// 门禁开门时间段2结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime2 { get; set; }
        /// <summary>
        /// 门禁开门时间段3开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime3 { get; set; }
        /// <summary>
        /// 门禁开门时间段3结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime3 { get; set; }
        /// <summary>
        /// 门禁开门时间段4开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime4 { get; set; }
        /// <summary>
        /// 门禁开门时间段4结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime4 { get; set; }



        /// <summary>
        /// 门禁开门时间段5开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime5 { get; set; }
        /// <summary>
        /// 门禁开门时间段5结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime5 { get; set; }
        /// <summary>
        /// 门禁开门时间段6开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime6 { get; set; }
        /// <summary>
        /// 门禁开门时间段6结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime6 { get; set; }
        /// <summary>
        /// 门禁开门时间段7开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime7 { get; set; }
        /// <summary>
        /// 门禁开门时间段7结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime7 { get; set; }
        /// <summary>
        /// 门禁开门时间段8开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime8 { get; set; }
        /// <summary>
        /// 门禁开门时间段8结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime8 { get; set; }
    }

    /// <summary>
    /// 假日计划数据（每一个机器或者学校分配）
    /// </summary>
    public class HolidayPlanData
    {

        /// <summary>
        /// 更新数据ID，用于操作成功返回用
        /// </summary>
        public int DataId { get; set; }


        /// <summary>
        /// HolidayGroupNo   假日分组编号 
        /// </summary>
        public int HolidayGroupNo { get; set; }


        /// <summary>
        /// 假日计划编号  必填  不能重复
        /// </summary>
        public int HolidayPlanNo { get; set; }


        /// <summary>
        /// 是否启用  xs: boolean, "1,0"   必填
        /// </summary>
        public int Enabled { get; set; }

        /// <summary>
        /// 群组名称（不能重复）
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 假日计划开始时间 年月日   2017-01-01T 00:00:00
        /// </summary>
        public string StartDate { get; set; }

        /// <summary>
        /// 假日计划结束时间 年月日   2017-01-01T 00:00:00
        /// </summary>
        public string EndDate { get; set; }

        /// <summary>
        /// 门禁开门时间段1开始时间（该时间点开始），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime1 { get; set; }

        /// <summary>
        /// 门禁开门时间段1结束时间（该时间点结束），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime1 { get; set; }

        /// <summary>
        /// 门禁开门时间段2开始时间（该时间点开始），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime2 { get; set; }

        /// <summary>
        /// 门禁开门时间段2结束时间（该时间点结束），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime2 { get; set; }

        /// <summary>
        /// 门禁开门时间段3开始时间（该时间点开始），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime3 { get; set; }

        /// <summary>
        /// 门禁开门时间段3结束时间（该时间点结束），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime3 { get; set; }

        /// <summary>
        /// 门禁开门时间段4开始时间（该时间点开始），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string fromTime4 { get; set; }

        /// <summary>
        /// 门禁开门时间段4结束时间（该时间点结束），格式为“时:分:秒”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string toTime4 { get; set; }

    }

    /// <summary>
    /// 假日组数据（每一个机器或者学校分配）
    /// </summary>
    public class HolidayGroupData
    {

        /// <summary>
        /// HolidayGroupNo   假日分组编号 
        /// </summary>
        public string HolidayGroupNo { get; set; }

        /// <summary>
        /// 是否启用  xs: boolean, "1,0"   必填
        /// </summary>
        public int Enabled { get; set; }

        /// <summary>
        /// 群组名称（不能重复）
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 假日计划数据
        /// </summary>
        public List<HolidayPeriodData> HolidayPeriodData { get; set; }

    }

    /// <summary>
    /// 假日计划数据
    /// </summary>
    public class HolidayPeriodData
    {

        /// <summary>
        /// 假日计划编号  必填  不能重复
        /// </summary>
        public string HolidayPlanNo { get; set; }

        /// <summary>
        /// 假日计划开始时间 年月日   2017-01-01T 00:00:00
        /// </summary>
        public string StartDate { get; set; }

        /// <summary>
        /// 假日计划结束时间 年月日   2017-01-01T 00:00:00
        /// </summary>
        public string EndDate { get; set; }

        /// <summary>
        /// 门禁开门时间段1开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime1 { get; set; }
        /// <summary>
        /// 门禁开门时间段1结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime1 { get; set; }
        /// <summary>
        /// 门禁开门时间段2开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime2 { get; set; }
        /// <summary>
        /// 门禁开门时间段2结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime2 { get; set; }
        /// <summary>
        /// 门禁开门时间段3开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime3 { get; set; }
        /// <summary>
        /// 门禁开门时间段3结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime3 { get; set; }
        /// <summary>
        /// 门禁开门时间段4开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime4 { get; set; }
        /// <summary>
        /// 门禁开门时间段4结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime4 { get; set; }



        /// <summary>
        /// 门禁开门时间段5开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime5 { get; set; }
        /// <summary>
        /// 门禁开门时间段5结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime5 { get; set; }
        /// <summary>
        /// 门禁开门时间段6开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime6 { get; set; }
        /// <summary>
        /// 门禁开门时间段6结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime6 { get; set; }
        /// <summary>
        /// 门禁开门时间段7开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime7 { get; set; }
        /// <summary>
        /// 门禁开门时间段7结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime7 { get; set; }
        /// <summary>
        /// 门禁开门时间段8开始时间（该时间点开始），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string FromTime8 { get; set; }
        /// <summary>
        /// 门禁开门时间段8结束时间（该时间点结束），格式为“时:分”，若为空字符串则为该时间段未做设置
        /// </summary>
        public string ToTime8 { get; set; }
    }

    //public class Param
    //{
        
    //    /// <summary>
    //    /// 事件消息 参见Events属性说明
    //    /// </summary>
    //    public List<Events> events { get; set; }

    //    /// <summary>
    //    /// 事件发送时间 采用ISO8601时间格式
    //    /// </summary>
    //    public DateTime sendTime { get; set; }
    //}
    public class Events
    {
        /// <summary>
        /// 事件的唯一id
        /// </summary>
        public string eventId { get; set; }

        /// <summary>
        /// 事件类型码  198914（合法卡比对通过 ） 人脸认证通过（196893）
        /// </summary>
        public string eventType { get; set; }

        /// <summary>
        /// 事件产生时间
        /// </summary>
        public string happenTime { get; set; }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string srcName { get; set; }

        /// <summary>
        /// 控制器设备唯一接入编码（平台机器唯一标识）
        /// </summary>
        public string srcParentIndex { get; set; }


        /// <summary>
        /// 资源类型（人脸机，安卓版）
        /// </summary>
        public string srcType { get; set; }

        /// <summary>
        /// 事件详情
        /// </summary>
        public ExtData data { get; set; }
    }
    public class ExtData
    {


        /// <summary>
        /// 进出类型  读卡器（0或null：不限，1:进，2：出）
        /// </summary>
        public int? ExtEventInOut { get; set; }
        /// <summary>
        /// 卡号
        /// </summary>
        public string ExtEventCardNo { get; set; }

        ///// <summary>
        ///// 事件类型代码
        ///// </summary>
        //public int ExtEventCode { get; set; }

        /// <summary>
        /// 人员编号，（平台唯一标识）
        /// </summary>
        public string ExtEventPersonNo { get; set; }

        /// <summary>
        /// 图片的url
        /// </summary>
        public string ExtEventPictureURL { get; set; }

        /// <summary>
        /// 用户类型
        /// </summary>
        public int UserType { get; set; }

        /// <summary>
        /// 刷卡时间
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// 图片的绝对地址
        /// </summary>
        public string ExtEventPicture { get; set; }
    }


    /// <summary>
    /// 海康请假同步数据
    /// </summary>
    public class HaiKangLeaveSyncData
    {
        /// <summary>
        /// 数据ID
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// 人员类型（01：学生，02：家长，03：职工）
        /// </summary>
        public string PersonType { get; set; }
        /// <summary>
        /// 平台人员ID
        /// </summary>
        public int PersonId { get; set; }
        /// <summary>
        /// 开始时间（格式：yyyy-MM-ddTHH:mm:ss）
        /// </summary>
        public string BeginTime { get; set; }
        /// <summary>
        /// 结束时间（格式：yyyy-MM-ddTHH:mm:ss）
        /// </summary>
        public string EndTime { get; set; }

        /// <summary>
        /// 计划模板
        /// </summary>
        public string PlanTemplateNo { get; set; }
    }
    /// <summary>
    /// 海康请假恢复同步数据
    /// </summary>
    public class HaiKangLeaveResetSyncData
    {
        /// <summary>
        /// 数据ID
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// 人员类型（01：学生，02：家长，03：职工）
        /// </summary>
        public string PersonType { get; set; }
        /// <summary>
        /// 平台人员ID
        /// </summary>
        public int PersonId { get; set; }
    }


    public class Person
    {
        /// <summary>
        /// 人员类型（01：学生，02：家长，03：职工）
        /// </summary>
        public string PersonType { get; set; }

        /// <summary>
        /// 平台人员ID
        /// </summary>
        public int PersonId { get; set; }

        /// <summary>
        /// 开始时间（格式：yyyy-MM-ddTHH:mm:ss）
        /// </summary>
        public string BeginTime { get; set; }

        /// <summary>
        /// 结束时间（格式：yyyy-MM-ddTHH:mm:ss）
        /// </summary>
        public string EndTime { get; set; }

        /// <summary>
        /// 计划模板
        /// </summary>
        public string PlanTemplateNo { get; set; }

        /// <summary>
        /// /*req, bool, 使能有效期, desc:false-不使能，true-使能*/
        /// </summary>
        public bool ValidEnable { get; set; }
    }


    /// <summary>
    /// 
    /// </summary>
    public class PutSyncDoorData
    {
        /// <summary>
        /// Id
        /// </summary>
        [DisplayName("Id")]
        public Guid Id { get; set; }

        /// <summary>
        ///  1=待下发，2=下发失败 ，3=下发成功
        /// </summary>
        public int? State { get; set; }


        /// <summary>
        /// 错误次数 超过5次不在下发
        /// </summary>
        public int? ErrorNumber { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [StringLength(255)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 错误时间
        /// </summary>
        public DateTime? ErrorTime { get; set; }

        /// <summary>
        /// 门禁操作结果
        /// </summary>
        public string OperationDoorResult { get; set; }
    }


    public class SyncData
    {
        /// <summary>
        /// 需要初始化
        /// </summary>
        public bool NeedInit { get; set; }


        /// <summary>
        /// 门禁数据需更新数量
        /// </summary>
        public int DoorCount { get; set; }

        /// <summary>
        /// 需要同步请假数量
        /// </summary>
        public int LeaveSyncCount { get; set; }

        /// <summary>
        /// 请假需要恢复数量
        /// </summary>
        public int LeaveResetSyncCount { get; set; }

        /// <summary>
        /// 人员数据
        /// </summary>
        public int AuthRulePersonData { get; set; }
    }


    /// <summary>
    /// 
    /// </summary>
    public class SyncPersonDataPage
    {
        /// <summary>
        /// ID
        /// </summary>
        [DisplayName("ID")]
        public Guid Id { get; set; }



        /// <summary>
        /// 人员ID
        /// </summary>
        [DisplayName("人员ID")]
        public int? PersonId { get; set; }

        /// <summary>
        /// 人员类型
        /// </summary>
        [DisplayName("人员类型")]
        public string PersonType { get; set; }

        /// <summary>
        /// 人员名称
        /// </summary>
        [DisplayName("人员名称")]
        public string PersonName { get; set; }

        /// <summary>
        /// 门禁Code,以英文逗号分隔，最多5个
        /// </summary>
        [DisplayName("门禁Code")]
        public string DoorCode { get; set; }


        /// <summary>
        /// 图片地址
        /// </summary>
        [StringLength(255)]
        [DisplayName("图片地址")]
        public string Pic { get; set; }

        /// <summary>
        /// 是否更新图片
        /// </summary>
        [DisplayName("是否更新图片")]
        public bool IsUpdatePic { get; set; }

        /// <summary>
        /// 机器id
        /// </summary>
        [DisplayName("机器id")]
        public int? MachineId { get; set; }

        /// <summary>
        /// 机器编码
        /// </summary>
        [DisplayName("机器编码")]
        public string MachineSN { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        [DisplayName("人员类型")]
        public bool? Sex { get; set; }


        /// <summary>
        /// 卡号
        /// </summary>
        [DisplayName("卡号")]
        public string CardId { get; set; } = "";




        /// <summary>
        /// 1是增加，2是修改，3是删除
        /// </summary>
        [DisplayName("Method")]
        public int? Method { get; set; }

        /// <summary>
        /// 源Id
        /// </summary>
        public string SourceId_List { get; set; }    // 存 list 的 Id

        /// <summary>
        /// 源Id1
        /// </summary>
        public string SourceId_List1 { get; set; }   // 存 list1 的 Id

        /// <summary>
        /// 添加时间
        /// </summary>
        [DisplayName("添加时间")]
        public DateTime AddTime { get; set; }

    }

    /// <summary>
    /// 
    /// </summary>
    public class PutSyncPersonData
    {
        /// <summary>
        /// Id
        /// </summary>
        [DisplayName("Id")]
        [Required(ErrorMessage = "请输入{0}的值")]
        public Guid? Id { get; set; }

        /// <summary>
        ///  1=待下发，2=下发失败 ，3=下发成功
        /// </summary>
        [DisplayName("状态")]
        [Required(ErrorMessage = "请输入{0}的值")]
        public int? State { get; set; }


        /// <summary>
        /// 错误次数 超过5次不在下发
        /// </summary>
        [DisplayName("错误次数 超过5次不在下发")]
        public int ErrorNumber { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [DisplayName("错误信息")]
        [StringLength(255)]
        public string ErrorMessage { get; set; }


        /// <summary>
        /// 下发人员信息结果
        /// </summary>
        public IssuePersonInfoResult IssuePersonInfoResult { get; set; }

        /// <summary>
        /// 下发人员卡号结果
        /// </summary>

        public IssuePersonCardResult IssuePersonCardResult { get; set; }

        /// <summary>
        /// 下发人员人脸结果
        /// </summary>

        public IssuePersonFaceResult IssuePersonFaceResult { get; set; }


        /// <summary>
        /// 源Id
        /// </summary>
        public string SourceId_List { get; set; }    // 存 list 的 Id

        /// <summary>
        /// 源Id1
        /// </summary>
        public string SourceId_List1 { get; set; }   // 存 list1 的 Id

        /// <summary>
        /// 添加时间（平台下发的时间，回传时原样带回）
        /// </summary>
        [DisplayName("添加时间")]
        public DateTime? AddTime { get; set; }
    }


    /// <summary>
    ///请假同步数据
    /// </summary>
    public class LeaveSyncData
    {
        /// <summary>
        /// 数据ID
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// 人员类型（01：学生，02：家长，03：职工）
        /// </summary>
        public string PersonType { get; set; }
        /// <summary>
        /// 平台人员ID
        /// </summary>
        public int PersonId { get; set; }
        /// <summary>
        /// 开始时间（格式：yyyy-MM-ddTHH:mm:ss）
        /// </summary>
        public string BeginTime { get; set; }
        /// <summary>
        /// 结束时间（格式：yyyy-MM-ddTHH:mm:ss）
        /// </summary>
        public string EndTime { get; set; }

        /// <summary>
        /// 门禁Code
        /// </summary>
        [DisplayName("门禁Code")]
        public string DoorCode { get; set; }
    }
    /// <summary>
    /// 请假恢复同步数据
    /// </summary>
    public class LeaveResetSyncData
    {
        /// <summary>
        /// 数据ID
        /// </summary>
        public Guid Id { get; set; }
        /// <summary>
        /// 人员类型（01：学生，02：家长，03：职工）
        /// </summary>
        public string PersonType { get; set; }
        /// <summary>
        /// 平台人员ID
        /// </summary>
        public int PersonId { get; set; }

        /// <summary>
        /// 门禁Code
        /// </summary>
        [DisplayName("门禁Code")]
        public string DoorCode { get; set; }
    }


    /// <summary>
    /// 
    /// </summary>
    public class PutSyncLeaveData
    {
        /// <summary>
        /// Id
        /// </summary>
        [DisplayName("Id")]
        [Required(ErrorMessage = "请输入{0}的值")]
        public Guid? Id { get; set; }

        /// <summary>
        ///  1=待下发，2=下发失败 ，3=下发成功
        /// </summary>
        [DisplayName("状态")]
        [Required(ErrorMessage = "请输入{0}的值")]
        public int? State { get; set; }


        /// <summary>
        /// 错误次数 超过5次不在下发
        /// </summary>
        [DisplayName("错误次数 超过5次不在下发")]
        public int ErrorNumber { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [DisplayName("错误信息")]
        [StringLength(255)]
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 错误时间
        /// </summary>
        [DisplayName("错误时间")]
        public DateTime? ErrorTime { get; set; }

    }

    /// <summary>
    /// 修改门禁数据
    /// </summary>
    public class PutDoorUpdateData
    {

        /// <summary>
        /// 成功数据ID
        /// </summary>
        [DisplayName("成功数据ID")]
        public List<int> SuccessDataIds { get; set; }

        /// <summary>
        /// 失败数据  
        /// </summary>
        [DisplayName("失败数据")]
        public List<ErrInfo> FailureData { get; set; }
    }
    public class ErrInfo
    {
        /// <summary>
        /// 数据ID
        /// </summary>
        public int DataId { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string Err { get; set; }


    }
    /// <summary>
    /// 设备操作结果（添加和修改操作）
    /// </summary>
    public class OperationMachineResult
    {
        /// <summary>
        /// <summary>
        /// 操作是否成功
        /// </summary>
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 添加人员信息结果
        /// </summary>
        public IssuePersonInfoResult IssuePersonInfoResult { get; set; }

        /// <summary>
        /// 添加人员卡号结果
        /// </summary>

        public IssuePersonCardResult IssuePersonCardResult { get; set; }

        /// <summary>
        /// 添加人员人脸结果
        /// </summary>

        public IssuePersonFaceResult IssuePersonFaceResult { get; set; }
    }
    /// <summary>
    /// 下发人员数据结果
    /// </summary>
    public class IssuePersonInfoResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 发送给设备的请求 JSON
        /// </summary>
        public string RequestJson { get; set; }

        /// <summary>
        /// 设备返回的响应 JSON
        /// </summary>
        public string ResponseJson { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 下发人员卡号结果
    /// </summary>
    public class IssuePersonCardResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 发送给设备的请求 JSON
        /// </summary>
        public string RequestJson { get; set; }

        /// <summary>
        /// 设备返回的响应 JSON
        /// </summary>
        public string ResponseJson { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }
    }
    /// <summary>
    /// 下发人员照片结果
    /// </summary>
    public class IssuePersonFaceResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 发送给设备的请求 JSON
        /// </summary>
        public string RequestJson { get; set; }

        /// <summary>
        /// 设备返回的响应 JSON
        /// </summary>
        public string ResponseJson { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 删除人员数据结果
    /// </summary>
    public class DelPersonDataResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 发送给设备的请求 JSON
        /// </summary>
        public string RequestJson { get; set; }

        /// <summary>
        /// 设备返回的响应 JSON
        /// </summary>
        public string ResponseJson { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }
    }



    /// <summary>
    /// 设备操作结果（添加和修改操作）
    /// </summary>
    public class OperationDoorResult
    {
        /// <summary>
        /// <summary>
        /// 操作是否成功
        /// </summary>
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 下发卡节假日数据结果
        /// </summary>
        public List<IssueCardHolidayGroup>? IssueCardHoliday { get; set; }

        /// <summary>
        /// 下发卡周计划数据结果
        /// </summary>
        public IssueCardDoorResult IssueCardTemplateResult { get; set; }

        /// <summary>
        /// 下发周计划时间点数据结果
        /// </summary>
        public IssueCardDoorResult IssueTimePeriodResult { get; set; }

    }

    /// <summary>
    /// 下发卡门禁数据结果
    /// </summary>
    public class IssueCardDoorResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 发送给设备的请求 XML
        /// </summary>
        public string RequestXML { get; set; }

        /// <summary>
        /// 设备返回的响应 XML
        /// </summary>
        public string ResponseXML { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }
    }


    /// <summary>
    /// 下发卡假日数据结果
    /// </summary>
    public class IssueCardHolidayGroup
    {
        /// <summary>
        /// HolidayGroupNo   假日分组编号 
        /// </summary>
        public string HolidayGroupNo { get; set; }


        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// 发送给设备的请求 XML
        /// </summary>
        public string RequestXML { get; set; }

        /// <summary>
        /// 设备返回的响应 XML
        /// </summary>
        public string ResponseXML { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string ErrorMessage { get; set; }


        /// <summary>
        /// 下发卡假日Plan结果
        /// </summary>
        public List<IssueCardDoorResult> IssueCardHolidayPlanResult { get; set; }
    }



}
