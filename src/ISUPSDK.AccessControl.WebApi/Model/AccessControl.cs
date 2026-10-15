

using System.Xml.Serialization;
using System.Collections.Generic;

namespace ISUPSDK.AccessControl.WebApi.Model
{
    /// <summary>
    /// 门禁控制能力集根节点，对应 ISAPI 的 AccessControl 接口。
    /// 用于查询设备支持的门禁功能，所有 isSupport 开头的布尔值表示是否支持对应功能。
    /// </summary>
    [XmlRoot("AccessControl", Namespace = "http://www.isapi.org/ver20/XMLSchema")]
    public class AccessControl
    {
        [XmlAttribute("version")]
        public string Version { get; set; } = "2.0";

        /// <summary>是否支持 Wiegand 配置（韦根协议参数）</summary>
        [XmlElement("isSupportWiegandCfg")]
        public bool IsSupportWiegandCfg { get; set; }

        /// <summary>是否支持模块状态查询</summary>
        [XmlElement("isSupportModuleStatus")]
        public bool IsSupportModuleStatus { get; set; }

        /// <summary>是否支持 SNAP 配置（简单网络管理协议相关）</summary>
        [XmlElement("isSupportSNAPConfig")]
        public bool IsSupportSNAPConfig { get; set; }

        /// <summary>是否支持身份终端功能</summary>
        [XmlElement("isSupportIdentityTerminal")]
        public bool IsSupportIdentityTerminal { get; set; }

        /// <summary>是否支持韦根规则配置</summary>
        [XmlElement("isSupportWiegandRuleCfg")]
        public bool IsSupportWiegandRuleCfg { get; set; }

        /// <summary>是否支持 M1 卡加密配置</summary>
        [XmlElement("isSupportM1CardEncryptCfg")]
        public bool IsSupportM1CardEncryptCfg { get; set; }

        /// <summary>是否支持部署信息查询</summary>
        [XmlElement("isSupportDeployInfo")]
        public bool IsSupportDeployInfo { get; set; }

        /// <summary>是否支持潜回（SubmarineBack，防跟随/反传）功能</summary>
        [XmlElement("isSupportSubmarineBack")]
        public bool IsSupportSubmarineBack { get; set; }

        /// <summary>是否支持服务器设备模式</summary>
        [XmlElement("isSupportServerDevice")]
        public bool IsSupportServerDevice { get; set; }

        /// <summary>是否支持读卡器跨主机</summary>
        [XmlElement("isSupportReaderAcrossHost")]
        public bool IsSupportReaderAcrossHost { get; set; }

        /// <summary>是否支持清空卡片记录</summary>
        [XmlElement("isSupportClearCardRecord")]
        public bool IsSupportClearCardRecord { get; set; }

        /// <summary>是否支持潜回模式配置</summary>
        [XmlElement("isSupportSubmarineBackMode")]
        public bool IsSupportSubmarineBackMode { get; set; }

        /// <summary>是否支持清除潜回记录</summary>
        [XmlElement("isSupportClearSubmarineBack")]
        public bool IsSupportClearSubmarineBack { get; set; }

        /// <summary>是否支持补光灯配置</summary>
        [XmlElement("isSupportSupplementLight")]
        public bool IsSupportSupplementLight { get; set; }

        /// <summary>是否支持人脸比对条件配置</summary>
        [XmlElement("isSupportFaceCompareCond")]
        public bool IsSupportFaceCompareCond { get; set; }

        /// <summary>是否支持远程开关门</summary>
        [XmlElement("isSupportRemoteControlDoor")]
        public bool IsSupportRemoteControlDoor { get; set; }

        /// <summary>是否支持人员信息管理</summary>
        [XmlElement("isSupportUserInfo")]
        public bool IsSupportUserInfo { get; set; }

        /// <summary>员工号配置信息（最小长度、最大长度及字符类型）</summary>
        [XmlElement("EmployeeNoInfo")]
        public EmployeeNoInfo EmployeeNoInfo { get; set; }

        /// <summary>是否支持卡片信息管理</summary>
        [XmlElement("isSupportCardInfo")]
        public bool IsSupportCardInfo { get; set; }

        /// <summary>是否支持人脸库（Face Library）</summary>
        [XmlElement("isSupportFDLib")]
        public bool IsSupportFDLib { get; set; }

        /// <summary>是否支持人员信息详情删除</summary>
        [XmlElement("isSupportUserInfoDetailDelete")]
        public bool IsSupportUserInfoDetailDelete { get; set; }

        /// <summary>是否支持指纹配置</summary>
        [XmlElement("isSupportFingerPrintCfg")]
        public bool IsSupportFingerPrintCfg { get; set; }

        /// <summary>是否支持指纹删除</summary>
        [XmlElement("isSupportFingerPrintDelete")]
        public bool IsSupportFingerPrintDelete { get; set; }

        /// <summary>是否支持门状态周计划配置</summary>
        [XmlElement("isSupportDoorStatusWeekPlanCfg")]
        public bool IsSupportDoorStatusWeekPlanCfg { get; set; }

        /// <summary>是否支持验证周计划配置</summary>
        [XmlElement("isSupportVerifyWeekPlanCfg")]
        public bool IsSupportVerifyWeekPlanCfg { get; set; }

        /// <summary>是否支持卡权限周计划配置</summary>
        [XmlElement("isSupportCardRightWeekPlanCfg")]
        public bool IsSupportCardRightWeekPlanCfg { get; set; }

        /// <summary>是否支持门状态假日计划配置</summary>
        [XmlElement("isSupportDoorStatusHolidayPlanCfg")]
        public bool IsSupportDoorStatusHolidayPlanCfg { get; set; }

        /// <summary>是否支持验证假日计划配置</summary>
        [XmlElement("isSupportVerifyHolidayPlanCfg")]
        public bool IsSupportVerifyHolidayPlanCfg { get; set; }

        /// <summary>是否支持卡权限假日计划配置</summary>
        [XmlElement("isSupportCardRightHolidayPlanCfg")]
        public bool IsSupportCardRightHolidayPlanCfg { get; set; }

        /// <summary>是否支持门状态假日组配置</summary>
        [XmlElement("isSupportDoorStatusHolidayGroupCfg")]
        public bool IsSupportDoorStatusHolidayGroupCfg { get; set; }

        /// <summary>是否支持验证假日组配置</summary>
        [XmlElement("isSupportVerifyHolidayGroupCfg")]
        public bool IsSupportVerifyHolidayGroupCfg { get; set; }

        /// <summary>是否支持用户权限假日组配置</summary>
        [XmlElement("isSupportUserRightHolidayGroupCfg")]
        public bool IsSupportUserRightHolidayGroupCfg { get; set; }

        /// <summary>是否支持门状态计划模板</summary>
        [XmlElement("isSupportDoorStatusPlanTemplate")]
        public bool IsSupportDoorStatusPlanTemplate { get; set; }

        /// <summary>是否支持验证计划模板</summary>
        [XmlElement("isSupportVerifyPlanTemplate")]
        public bool IsSupportVerifyPlanTemplate { get; set; }

        /// <summary>是否支持用户权限计划模板</summary>
        [XmlElement("isSupportUserRightPlanTemplate")]
        public bool IsSupportUserRightPlanTemplate { get; set; }

        /// <summary>是否支持门状态计划</summary>
        [XmlElement("isSupportDoorStatusPlan")]
        public bool IsSupportDoorStatusPlan { get; set; }

        /// <summary>是否支持读卡器计划</summary>
        [XmlElement("isSupportCardReaderPlan")]
        public bool IsSupportCardReaderPlan { get; set; }

        /// <summary>是否支持清除计划配置</summary>
        [XmlElement("isSupportClearPlansCfg")]
        public bool IsSupportClearPlansCfg { get; set; }

        /// <summary>是否支持事件卡号列表</summary>
        [XmlElement("isSupportEventCardNoList")]
        public bool IsSupportEventCardNoList { get; set; }

        /// <summary>是否支持事件卡片联动配置</summary>
        [XmlElement("isSupportEventCardLinkageCfg")]
        public bool IsSupportEventCardLinkageCfg { get; set; }

        /// <summary>是否支持清除事件卡片联动配置</summary>
        [XmlElement("isSupportClearEventCardLinkageCfg")]
        public bool IsSupportClearEventCardLinkageCfg { get; set; }

        /// <summary>是否支持门禁事件查询</summary>
        [XmlElement("isSupportAcsEvent")]
        public bool IsSupportAcsEvent { get; set; }

        /// <summary>是否支持门禁事件总数查询</summary>
        [XmlElement("isSupportAcsEventTotalNum")]
        public bool IsSupportAcsEventTotalNum { get; set; }

        /// <summary>是否支持事件优化配置</summary>
        [XmlElement("isSupportEventOptimizationCfg")]
        public bool IsSupportEventOptimizationCfg { get; set; }

        /// <summary>是否支持门禁工作状态查询</summary>
        [XmlElement("isSupportAcsWorkStatus")]
        public bool IsSupportAcsWorkStatus { get; set; }

        /// <summary>是否支持门配置</summary>
        [XmlElement("isSupportDoorCfg")]
        public bool IsSupportDoorCfg { get; set; }

        /// <summary>是否支持读卡器配置</summary>
        [XmlElement("isSupportCardReaderCfg")]
        public bool IsSupportCardReaderCfg { get; set; }

        /// <summary>是否支持门禁配置</summary>
        [XmlElement("isSupportAcsCfg")]
        public bool IsSupportAcsCfg { get; set; }

        /// <summary>是否支持远程检查（如设备在线状态）</summary>
        [XmlElement("isSupportRemoteCheck")]
        public bool IsSupportRemoteCheck { get; set; }

        /// <summary>是否支持口罩检测</summary>
        [XmlElement("isSupportMaskDetection")]
        public bool IsSupportMaskDetection { get; set; }

        /// <summary>是否支持分组配置</summary>
        [XmlElement("isSupportGroupCfg")]
        public bool IsSupportGroupCfg { get; set; }

        /// <summary>是否支持清除分组配置</summary>
        [XmlElement("isSupportClearGroupCfg")]
        public bool IsSupportClearGroupCfg { get; set; }

        /// <summary>是否支持多卡认证配置</summary>
        [XmlElement("isSupportMultiCardCfg")]
        public bool IsSupportMultiCardCfg { get; set; }

        /// <summary>是否支持多门互锁配置</summary>
        [XmlElement("isSupportMultiDoorInterLockCfg")]
        public bool IsSupportMultiDoorInterLockCfg { get; set; }

        /// <summary>是否支持防潜回配置（反传）</summary>
        [XmlElement("isSupportAntiSneakCfg")]
        public bool IsSupportAntiSneakCfg { get; set; }

        /// <summary>是否支持读卡器防潜回配置</summary>
        [XmlElement("isSupportCardReaderAntiSneakCfg")]
        public bool IsSupportCardReaderAntiSneakCfg { get; set; }

        /// <summary>是否支持清除防潜回配置</summary>
        [XmlElement("isSupportClearAntiSneakCfg")]
        public bool IsSupportClearAntiSneakCfg { get; set; }

        /// <summary>是否支持清除防潜回记录</summary>
        [XmlElement("isSupportClearAntiSneak")]
        public bool IsSupportClearAntiSneak { get; set; }

        /// <summary>是否支持 OSDP 状态查询（开放监控设备协议）</summary>
        [XmlElement("isSupportOSDPStatus")]
        public bool IsSupportOSDPStatus { get; set; }

        /// <summary>是否支持 OSDP 参数修改</summary>
        [XmlElement("isSupportOSDPModify")]
        public bool IsSupportOSDPModify { get; set; }

        /// <summary>是否支持二维码加密</summary>
        [XmlElement("isSupportQRCodeEncryption")]
        public bool IsSupportQRCodeEncryption { get; set; }

        /// <summary>是否支持二维码信息管理</summary>
        [XmlElement("isSupportQRCodeInfo")]
        public bool IsSupportQRCodeInfo { get; set; }

        /// <summary>是否支持蓝牙加密信息配置</summary>
        [XmlElement("isSupportBluetoothEncryptionInfo")]
        public bool IsSupportBluetoothEncryptionInfo { get; set; }

        /// <summary>是否支持蓝牙加密版本配置</summary>
        [XmlElement("isSupportBluetoothEncryptionVersion")]
        public bool IsSupportBluetoothEncryptionVersion { get; set; }

        /// <summary>恢复出厂设置能力</summary>
        [XmlElement("FactoryReset")]
        public FactoryReset FactoryReset { get; set; }

        /// <summary>是否支持抓拍可见光人脸图</summary>
        [XmlElement("isSupportCaptureFace")]
        public bool IsSupportCaptureFace { get; set; }

        /// <summary>是否支持抓拍红外热像人脸图</summary>
        [XmlElement("isSupportCaptureInfraredFace")]
        public bool IsSupportCaptureInfraredFace { get; set; }

        /// <summary>是否支持人脸识别模式配置</summary>
        [XmlElement("isSupportFaceRecognizeMode")]
        public bool IsSupportFaceRecognizeMode { get; set; }

        /// <summary>是否支持抓拍卡片信息</summary>
        [XmlElement("isSupportCaptureCardInfo")]
        public bool IsSupportCaptureCardInfo { get; set; }

        /// <summary>是否支持 NFC 配置</summary>
        [XmlElement("isSupportNFCCfg")]
        public bool IsSupportNFCCfg { get; set; }

        /// <summary>是否支持 RF 卡片配置（射频卡）</summary>
        [XmlElement("isSupportRFCardCfg")]
        public bool IsSupportRFCardCfg { get; set; }

        /// <summary>是否支持 TTS 文本播报（文本转语音）</summary>
        [XmlElement("isSupportTTSText")]
        public bool IsSupportTTSText { get; set; }

        /// <summary>是否支持身份证黑名单配置</summary>
        [XmlElement("isSupportIDBlackListCfg")]
        public bool IsSupportIDBlackListCfg { get; set; }

        /// <summary>是否支持维护数据导出</summary>
        [XmlElement("isSupportMaintenanceDataExport")]
        public bool IsSupportMaintenanceDataExport { get; set; }

        /// <summary>是否支持锁类型配置</summary>
        [XmlElement("isSupportLockTypeCfg")]
        public bool IsSupportLockTypeCfg { get; set; }

        /// <summary>是否支持安全帽检测</summary>
        [XmlElement("isSupportSafetyHelmetDetection")]
        public bool IsSupportSafetyHelmetDetection { get; set; }

        /// <summary>是否支持按键配置考勤</summary>
        [XmlElement("isSupportKeyCfgAttendance")]
        public bool IsSupportKeyCfgAttendance { get; set; }

        /// <summary>是否支持异步导入数据</summary>
        [XmlElement("isSupportAsyncImportDatas")]
        public bool IsSupportAsyncImportDatas { get; set; }

        /// <summary>是否支持异步导入图片</summary>
        [XmlElement("isSupportAsyncImportPic")]
        public bool IsSupportAsyncImportPic { get; set; }

        /// <summary>是否支持清空身份证黑名单</summary>
        [XmlElement("isSupportIDBlackListClear")]
        public bool IsSupportIDBlackListClear { get; set; }

        /// <summary>是否支持身份证黑名单参数配置</summary>
        [XmlElement("isSupportIDBlackListParamCfg")]
        public bool IsSupportIDBlackListParamCfg { get; set; }

        /// <summary>是否支持指纹数据加密</summary>
        [XmlElement("isSupportFingerDataEncryption")]
        public bool IsSupportFingerDataEncryption { get; set; }

        /// <summary>是否支持考勤周计划</summary>
        [XmlElement("isSupportAttendanceWeekPlan")]
        public bool IsSupportAttendanceWeekPlan { get; set; }

        /// <summary>是否支持清除考勤计划</summary>
        [XmlElement("isSupportClearAttendancePlan")]
        public bool IsSupportClearAttendancePlan { get; set; }

        /// <summary>是否支持考勤状态计划</summary>
        [XmlElement("isSupportAttendanceStatusPlan")]
        public bool IsSupportAttendanceStatusPlan { get; set; }

        /// <summary>是否支持考勤模式配置</summary>
        [XmlElement("isSupportAttendanceMode")]
        public bool IsSupportAttendanceMode { get; set; }

        /// <summary>是否支持考勤计划模板</summary>
        [XmlElement("isSupportAttendancePlanTemplate")]
        public bool IsSupportAttendancePlanTemplate { get; set; }

        /// <summary>是否支持考勤计划模板列表查询</summary>
        [XmlElement("isSupportAttendancePlanTemplateList")]
        public bool IsSupportAttendancePlanTemplateList { get; set; }

        /// <summary>是否支持清除图片配置</summary>
        [XmlElement("isSupportClearPictureCfg")]
        public bool IsSupportClearPictureCfg { get; set; }

        /// <summary>是否支持事件存储配置</summary>
        [XmlElement("isSupportEventStorageCfg")]
        public bool IsSupportEventStorageCfg { get; set; }

        /// <summary>是否支持卡片验证规则</summary>
        [XmlElement("isSupportCardVerificationRule")]
        public bool IsSupportCardVerificationRule { get; set; }

        /// <summary>是否支持身份证信息事件</summary>
        [XmlElement("isSupportIDCardInfoEvent")]
        public bool IsSupportIDCardInfoEvent { get; set; }

        /// <summary>是否支持蓝牙功能</summary>
        [XmlElement("isSupportBluetooth")]
        public bool IsSupportBluetooth { get; set; }

        /// <summary>是否支持跨主机互锁服务端</summary>
        [XmlElement("isSupportAcrossHostInterlockServer")]
        public bool IsSupportAcrossHostInterlockServer { get; set; }

        /// <summary>是否支持跨主机互锁主机信息配置</summary>
        [XmlElement("isSupportAcrossHostInterlockHostInfo")]
        public bool IsSupportAcrossHostInterlockHostInfo { get; set; }

        /// <summary>是否支持跨主机互锁组配置</summary>
        [XmlElement("isSupportAcrossHostInterlockGroup")]
        public bool IsSupportAcrossHostInterlockGroup { get; set; }

        /// <summary>是否支持跨主机互锁门配置</summary>
        [XmlElement("isSupportAcrossHostInterlockDoor")]
        public bool IsSupportAcrossHostInterlockDoor { get; set; }

        /// <summary>是否支持跨主机互锁客户端</summary>
        [XmlElement("isSupportAcrossHostInterlockClient")]
        public bool IsSupportAcrossHostInterlockClient { get; set; }

        /// <summary>是否支持用户和权限显示</summary>
        [XmlElement("isSupportUserAndRightShow")]
        public bool IsSupportUserAndRightShow { get; set; }

        /// <summary>是否支持添加自定义语音</summary>
        [XmlElement("isSupportAddCustomAudio")]
        public bool IsSupportAddCustomAudio { get; set; }

        /// <summary>是否支持删除自定义语音</summary>
        [XmlElement("isSupportDeleteCustomAudio")]
        public bool IsSupportDeleteCustomAudio { get; set; }

        /// <summary>是否支持搜索自定义语音</summary>
        [XmlElement("isSupportSearchCustomAudio")]
        public bool IsSupportSearchCustomAudio { get; set; }

        /// <summary>是否支持删除事件卡片联动配置</summary>
        [XmlElement("isSupportEventCardLinkageCfgDelete")]
        public bool IsSupportEventCardLinkageCfgDelete { get; set; }

        /// <summary>是否支持自定义提示</summary>
        [XmlElement("isSupportCustomPrompt")]
        public bool IsSupportCustomPrompt { get; set; }

        /// <summary>是否支持清除用户认证次数</summary>
        [XmlElement("isSupportClearUserAuthenticationTimes")]
        public bool IsSupportClearUserAuthenticationTimes { get; set; }

        /// <summary>是否支持查询模型失败记录</summary>
        [XmlElement("isSupportSearchModelFailRecord")]
        public bool IsSupportSearchModelFailRecord { get; set; }

        /// <summary>是否支持身份证软件方案参数配置</summary>
        [XmlElement("isSupportIDCardSoftSolutionParams")]
        public bool IsSupportIDCardSoftSolutionParams { get; set; }

        /// <summary>是否支持本地用户变更记录参数配置</summary>
        [XmlElement("isSupportLocalUserChangeRecordParam")]
        public bool IsSupportLocalUserChangeRecordParam { get; set; }

        /// <summary>是否支持查询本地用户变更</summary>
        [XmlElement("isSupportSearchLocalUserChange")]
        public bool IsSupportSearchLocalUserChange { get; set; }

        /// <summary>是否支持读卡器认证模式</summary>
        [XmlElement("isSupportReaderAuthenticationMode")]
        public bool IsSupportReaderAuthenticationMode { get; set; }

        /// <summary>是否支持本地用户变更确认</summary>
        [XmlElement("isSupportLocalUserChangeAcknowledgment")]
        public bool IsSupportLocalUserChangeAcknowledgment { get; set; }

        /// <summary>是否支持校验工程模式安全码</summary>
        [XmlElement("isSupportCheckEngineeringModeSecurityCode")]
        public bool IsSupportCheckEngineeringModeSecurityCode { get; set; }

        /// <summary>是否支持读卡器工程模式参数配置</summary>
        [XmlElement("isSupportReaderEngineeringModeParams")]
        public bool IsSupportReaderEngineeringModeParams { get; set; }

        /// <summary>是否支持密码管理参数配置</summary>
        [XmlElement("isSupportPwMgrParams")]
        public bool IsSupportPwMgrParams { get; set; }

        /// <summary>是否支持获取用户可用密码</summary>
        [XmlElement("isSupportGetUserAvailablePw")]
        public bool IsSupportGetUserAvailablePw { get; set; }

        /// <summary>是否支持用户可用密码查询确认</summary>
        [XmlElement("isSupportUserAvailablePwQueryAcknowledgment")]
        public bool IsSupportUserAvailablePwQueryAcknowledgment { get; set; }

        /// <summary>是否支持解锁防攻击</summary>
        [XmlElement("isSupportUnlockAntiAttack")]
        public bool IsSupportUnlockAntiAttack { get; set; }

        /// <summary>是否支持自定义人员类型</summary>
        [XmlElement("isSupportCustomPersonnelType")]
        public bool IsSupportCustomPersonnelType { get; set; }

        /// <summary>是否支持远程控制信息</summary>
        [XmlElement("isSupportRemoteControlInfo")]
        public bool IsSupportRemoteControlInfo { get; set; }

        /// <summary>是否支持门禁安全模块开关参数配置</summary>
        [XmlElement("isSupportDoorSecurityModuleSwitchParams")]
        public bool IsSupportDoorSecurityModuleSwitchParams { get; set; }

        /// <summary>是否支持触摸识别参数配置</summary>
        [XmlElement("isSupportTouchRecognitionParams")]
        public bool IsSupportTouchRecognitionParams { get; set; }

        /// <summary>是否支持获取用户支持的远程控制信息</summary>
        [XmlElement("isSupportGetUserSupportRemoteControlInfo")]
        public bool IsSupportGetUserSupportRemoteControlInfo { get; set; }

        /// <summary>是否支持获取远程控制配对状态</summary>
        [XmlElement("isSupportGetRemoteControlPairStatus")]
        public bool IsSupportGetRemoteControlPairStatus { get; set; }

        /// <summary>不关联读卡器的门列表（例如门 ID 2 的读卡器独立工作）</summary>
        [XmlElement("notRelateReaderDoorIDList")]
        public NotRelateReaderDoorIDList NotRelateReaderDoorIDList { get; set; }

        /// <summary>是否支持获取 NFC 加密信息</summary>
        [XmlElement("isSupportGetNFCEncryptionInfo")]
        public bool IsSupportGetNFCEncryptionInfo { get; set; }

        /// <summary>是否支持修改 NFC 加密信息</summary>
        [XmlElement("isSupportModifyNFCEncryptionInfo")]
        public bool IsSupportModifyNFCEncryptionInfo { get; set; }

        /// <summary>是否支持获取显示模式缩略图列表</summary>
        [XmlElement("isSupportGetShowModeThumbnailsList")]
        public bool IsSupportGetShowModeThumbnailsList { get; set; }

        /// <summary>是否支持更新二维码有效期</summary>
        [XmlElement("isSupportUpdateQRCodeEffectiveTime")]
        public bool IsSupportUpdateQRCodeEffectiveTime { get; set; }

        /// <summary>是否支持自定义音频参数</summary>
        [XmlElement("isSupportCustomAudioParams")]
        public bool IsSupportCustomAudioParams { get; set; }

        /// <summary>是否支持删除互锁客户端设备</summary>
        [XmlElement("isSupportDeleteInterLockClientDevice")]
        public bool IsSupportDeleteInterLockClientDevice { get; set; }

        /// <summary>是否支持查询互锁客户端</summary>
        [XmlElement("isSupportSearchInterLockClient")]
        public bool IsSupportSearchInterLockClient { get; set; }

        /// <summary>是否支持查询互锁客户端门</summary>
        [XmlElement("isSupportSearchInterLockClientDoor")]
        public bool IsSupportSearchInterLockClientDoor { get; set; }
    }

    /// <summary>
    /// 员工号配置信息，定义员工编号的长度和字符类型限制。
    /// </summary>
    public class EmployeeNoInfo
    {
        /// <summary>员工号长度及取值范围（min~max）</summary>
        [XmlElement("employeeNo")]
        public EmployeeNo EmployeeNo { get; set; }

        /// <summary>允许的字符类型，opt 属性表示可选值（如 any 表示任意字符）</summary>
        [XmlElement("characterType")]
        public CharacterType CharacterType { get; set; }
    }

    /// <summary>
    /// 员工号长度定义，包含最小长度、最大长度以及实际值（通常为默认长度）。
    /// </summary>
    public class EmployeeNo
    {
        /// <summary>最小长度</summary>
        [XmlAttribute("min")]
        public int Min { get; set; }

        /// <summary>最大长度</summary>
        [XmlAttribute("max")]
        public int Max { get; set; }

        /// <summary>实际值（如示例中的 1）</summary>
        [XmlText]
        public string Value { get; set; }
    }

    /// <summary>
    /// 员工号字符类型，opt 属性指示允许的字符集（例如 any 表示任意字符）。
    /// </summary>
    public class CharacterType
    {
        /// <summary>字符类型选项，常见值：any（任意）、number（数字）、letter（字母）等</summary>
        [XmlAttribute("opt")]
        public string Opt { get; set; }
    }

    /// <summary>
    /// 恢复出厂设置能力，指示是否支持以及支持的模式。
    /// </summary>
    public class FactoryReset
    {
        /// <summary>是否支持恢复出厂设置</summary>
        [XmlElement("isSupportFactoryReset")]
        public bool IsSupportFactoryReset { get; set; }

        /// <summary>恢复模式：full（完全重置）、basic（基本重置）、part（部分重置）</summary>
        [XmlAttribute("mode")]
        public string Mode { get; set; }
    }

    /// <summary>
    /// 不关联读卡器的门列表，某些门可能没有直接关联的读卡器（例如仅用作逻辑门）。
    /// </summary>
    public class NotRelateReaderDoorIDList
    {
        /// <summary>不关联读卡器的门项列表</summary>
        [XmlElement("notRelateReaderDoorID")]
        public List<NotRelateReaderDoorID> Items { get; set; }
    }

    /// <summary>
    /// 不关联读卡器的门项，包含门 ID。
    /// </summary>
    public class NotRelateReaderDoorID
    {
        /// <summary>门编号（例如示例中的 2）</summary>
        [XmlElement("doorID")]
        public int DoorID { get; set; }
    }


    /// <summary>
    /// 用户信息能力，指示是否支持用户信息功能。
    /// </summary>
    public class UserInfoCapabilities
    {

    }
}