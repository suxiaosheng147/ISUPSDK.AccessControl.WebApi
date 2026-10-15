
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class InfoListItem
{
    public int major { get; set; }

    public int minor { get; set; }

    public string time { get; set; }

    //public string netUser { get; set; }

    //public string remoteHostAddr { get; set; }

    public string cardNo { get; set; }

    public int cardType { get; set; }

   // public int whiteListNo { get; set; }

    //public int reportChannel { get; set; }

    //public int cardReaderKind { get; set; }

    //public int cardReaderNo { get; set; }

    public int doorNo { get; set; }

   // public int verifyNo { get; set; }

    //public int alarmInNo { get; set; }

    //public int alarmOutNo { get; set; }

    public int caseSensorNo { get; set; }

    //public int RS485No { get; set; }

    //public int multiCardGroupNo { get; set; }

    //public int accessChannel { get; set; }

    //public string deviceNo { get; set; }

    //public int distractControlNo { get; set; }

    public string employeeNoString { get; set; }

    //public int localControllerID { get; set; }

    //public int InternetAccess { get; set; }

    public int type { get; set; }

    /// <summary>
    /// public string MACAddr { get; set; }
    /// </summary>

   // public int swipeCardType { get; set; }

    public int serialNo { get; set; }

    //public int channelControllerID { get; set; }

    //public int channelControllerLampID { get; set; }

    //public int channelControllerIRAdaptorID { get; set; }

    //public int channelControllerIREmitterID { get; set; }

    public string userType { get; set; }

    public string currentVerifyMode { get; set; }

    //public string filename { get; set; }

    //public string pictureURL { get; set; }

}

public class AcsEvent
{
    public string searchID { get; set; }

    public string responseStatusStrg { get; set; }

    public int numOfMatches { get; set; }

    public int totalMatches { get; set; }

    public List<InfoListItem> InfoList { get; set; }

}

public class AcsEventObject
{
    public AcsEvent AcsEvent { get; set; }
}

class GetAcsEventType
{
    public static uint ReturnMinorTypeValue(ref string IndexValue)
    {
        if (MinorTypeDictionary.ContainsKey(IndexValue))
        {
            return MinorTypeDictionary[IndexValue];
        }
        return 0;
    }

    public static uint ReturnMajorTypeValue(ref string IndexValue)
    {
        if (MajorTypeDictionary.ContainsKey(IndexValue))
        {
            return MajorTypeDictionary[IndexValue];
        }
        return 0;
    }

    public static ushort ReturnInductiveEventTypeValue(string IndexValue)
    {
        if (ComInductiveEvent.ContainsKey(IndexValue))
        {
            return ComInductiveEvent[IndexValue];
        }
        return 0xffff;
    }

    public static int ReturnSearchValue(string IndexValue)
    {
        if (SearchType.ContainsKey(IndexValue))
        {
            return SearchType[IndexValue];
        }
        return 0;
    }
    private static Dictionary<string, uint> MinorTypeDictionary = new Dictionary<string, uint>()
        {
            {"All",0},
            //{"ALARMIN_SHORT_CIRCUIT",EHDemoPublic.MINOR_ALARMIN_SHORT_CIRCUIT},
            //{"ALARMIN_BROKEN_CIRCUIT",EHDemoPublic.MINOR_ALARMIN_BROKEN_CIRCUIT},
            //{"ALARMIN_EXCEPTION",EHDemoPublic.MINOR_ALARMIN_EXCEPTION},
            //{"ALARMIN_RESUME",EHDemoPublic.MINOR_ALARMIN_RESUME},
            //{"HOST_DESMANTLE_ALARM",EHDemoPublic.MINOR_HOST_DESMANTLE_ALARM},
            //{"HOST_DESMANTLE_RESUME",EHDemoPublic.MINOR_HOST_DESMANTLE_RESUME},
            //{"CARD_READER_DESMANTLE_ALARM",EHDemoPublic.MINOR_CARD_READER_DESMANTLE_ALARM},
            //{"CARD_READER_DESMANTLE_RESUME",EHDemoPublic.MINOR_CARD_READER_DESMANTLE_RESUME},
            //{"CASE_SENSOR_ALARM",EHDemoPublic.MINOR_CASE_SENSOR_ALARM},
            //{"CASE_SENSOR_RESUME",EHDemoPublic.MINOR_CASE_SENSOR_RESUME},
            //{"STRESS_ALARM",EHDemoPublic.MINOR_STRESS_ALARM},
            //{"OFFLINE_ECENT_NEARLY_FULL",EHDemoPublic.MINOR_OFFLINE_ECENT_NEARLY_FULL},
            //{"CARD_MAX_AUTHENTICATE_FAIL",EHDemoPublic.MINOR_CARD_MAX_AUTHENTICATE_FAIL},
            //{"SD_CARD_FULL",EHDemoPublic.MINOR_SD_CARD_FULL},
            //{"LINKAGE_CAPTURE_PIC",EHDemoPublic.MINOR_LINKAGE_CAPTURE_PIC},
            //{"SECURITY_MODULE_DESMANTLE_ALARM",EHDemoPublic.MINOR_SECURITY_MODULE_DESMANTLE_ALARM},
            //{"SECURITY_MODULE_DESMANTLE_RESUME",EHDemoPublic.MINOR_SECURITY_MODULE_DESMANTLE_RESUME},
            //{"POS_START_ALARM",EHDemoPublic.MINOR_POS_START_ALARM},
            //{"POS_END_ALARM",EHDemoPublic.MINOR_POS_END_ALARM},
            //{"FACE_IMAGE_QUALITY_LOW",EHDemoPublic.MINOR_FACE_IMAGE_QUALITY_LOW},
            //{"FINGE_RPRINT_QUALITY_LOW",EHDemoPublic.MINOR_FINGE_RPRINT_QUALITY_LOW},
            //{"FIRE_IMPORT_BROKEN_CIRCUIT",EHDemoPublic.MINOR_FIRE_IMPORT_BROKEN_CIRCUIT},
            //{"FIRE_IMPORT_RESUME",EHDemoPublic.MINOR_FIRE_IMPORT_RESUME},
            //{"FIRE_BUTTON_TRIGGER",EHDemoPublic.MINOR_FIRE_BUTTON_TRIGGER},
            //{"FIRE_BUTTON_RESUME",EHDemoPublic.MINOR_FIRE_BUTTON_RESUME},
            //{"MAINTENANCE_BUTTON_TRIGGER",EHDemoPublic.MINOR_MAINTENANCE_BUTTON_TRIGGER},
            //{"MAINTENANCE_BUTTON_RESUME",EHDemoPublic.MINOR_MAINTENANCE_BUTTON_RESUME},
            //{"EMERGENCY_BUTTON_TRIGGER",EHDemoPublic.MINOR_EMERGENCY_BUTTON_TRIGGER},
            //{"EMERGENCY_BUTTON_RESUME",EHDemoPublic.MINOR_EMERGENCY_BUTTON_RESUME},
            //{"DISTRACT_CONTROLLER_ALARM",EHDemoPublic.MINOR_DISTRACT_CONTROLLER_ALARM},
            //{"DISTRACT_CONTROLLER_RESUME",EHDemoPublic.MINOR_DISTRACT_CONTROLLER_RESUME},
            //{"CHANNEL_CONTROLLER_DESMANTLE_ALARM",EHDemoPublic.MINOR_CHANNEL_CONTROLLER_DESMANTLE_ALARM},
            //{"CHANNEL_CONTROLLER_DESMANTLE_RESUME",EHDemoPublic.MINOR_CHANNEL_CONTROLLER_DESMANTLE_RESUME},
            //{"CHANNEL_CONTROLLER_FIRE_IMPORT_ALARM",EHDemoPublic.MINOR_CHANNEL_CONTROLLER_FIRE_IMPORT_ALARM},
            //{"CHANNEL_CONTROLLER_FIRE_IMPORT_RESUME",EHDemoPublic.MINOR_CHANNEL_CONTROLLER_FIRE_IMPORT_RESUME},
            //{"PRINTER_OUT_OF_PAPER",EHDemoPublic.MINOR_PRINTER_OUT_OF_PAPER},
            //{"LEGAL_EVENT_NEARLY_FULL",EHDemoPublic.MINOR_LEGAL_EVENT_NEARLY_FULL},
            //{"NET_BROKEN",EHDemoPublic.MINOR_NET_BROKEN},
            //{"RS485_DEVICE_ABNORMAL",EHDemoPublic.MINOR_RS485_DEVICE_ABNORMAL},
            //{"RS485_DEVICE_REVERT",EHDemoPublic.MINOR_RS485_DEVICE_REVERT},
            //{"DEV_POWER_ON",EHDemoPublic.MINOR_DEV_POWER_ON},
            //{"DEV_POWER_OFF",EHDemoPublic.MINOR_DEV_POWER_OFF},
            //{"WATCH_DOG_RESET",EHDemoPublic.MINOR_WATCH_DOG_RESET},
            //{"LOW_BATTERY",EHDemoPublic.MINOR_LOW_BATTERY},
            //{"BATTERY_RESUME",EHDemoPublic.MINOR_BATTERY_RESUME},
            //{"AC_OFF",EHDemoPublic.MINOR_AC_OFF},
            //{"AC_RESUME",EHDemoPublic.MINOR_AC_RESUME},
            //{"NET_RESUME",EHDemoPublic.MINOR_NET_RESUME},
            //{"FLASH_ABNORMAL",EHDemoPublic.MINOR_FLASH_ABNORMAL},
            //{"CARD_READER_OFFLINE",EHDemoPublic.MINOR_CARD_READER_OFFLINE},
            //{"CARD_READER_RESUME",EHDemoPublic.MINOR_CARD_READER_RESUME},
            //{"INDICATOR_LIGHT_OFF",EHDemoPublic.MINOR_INDICATOR_LIGHT_OFF},
            //{"INDICATOR_LIGHT_RESUME",EHDemoPublic.MINOR_INDICATOR_LIGHT_RESUME},
            //{"CHANNEL_CONTROLLER_OFF",EHDemoPublic.MINOR_CHANNEL_CONTROLLER_OFF},
            //{"CHANNEL_CONTROLLER_RESUME",EHDemoPublic.MINOR_CHANNEL_CONTROLLER_RESUME},
            //{"SECURITY_MODULE_OFF",EHDemoPublic.MINOR_SECURITY_MODULE_OFF},
            //{"SECURITY_MODULE_RESUME",EHDemoPublic.MINOR_SECURITY_MODULE_RESUME},
            //{"BATTERY_ELECTRIC_LOW",EHDemoPublic.MINOR_BATTERY_ELECTRIC_LOW},
            //{"BATTERY_ELECTRIC_RESUME",EHDemoPublic.MINOR_BATTERY_ELECTRIC_RESUME},
            //{"LOCAL_CONTROL_NET_BROKEN",EHDemoPublic.MINOR_LOCAL_CONTROL_NET_BROKEN},
            //{"LOCAL_CONTROL_NET_RSUME",EHDemoPublic.MINOR_LOCAL_CONTROL_NET_RSUME},
            //{"MASTER_RS485_LOOPNODE_BROKEN",EHDemoPublic.MINOR_MASTER_RS485_LOOPNODE_BROKEN},
            //{"MASTER_RS485_LOOPNODE_RESUME",EHDemoPublic.MINOR_MASTER_RS485_LOOPNODE_RESUME},
            //{"LOCAL_CONTROL_OFFLINE",EHDemoPublic.MINOR_LOCAL_CONTROL_OFFLINE},
            //{"LOCAL_CONTROL_RESUME",EHDemoPublic.MINOR_LOCAL_CONTROL_RESUME},
            //{"LOCAL_DOWNSIDE_RS485_LOOPNODE_BROKEN",EHDemoPublic.MINOR_LOCAL_DOWNSIDE_RS485_LOOPNODE_BROKEN},
            //{"LOCAL_DOWNSIDE_RS485_LOOPNODE_RESUME",EHDemoPublic.MINOR_LOCAL_DOWNSIDE_RS485_LOOPNODE_RESUME},
            //{"DISTRACT_CONTROLLER_ONLINE",EHDemoPublic.MINOR_DISTRACT_CONTROLLER_ONLINE},
            //{"DISTRACT_CONTROLLER_OFFLINE",EHDemoPublic.MINOR_DISTRACT_CONTROLLER_OFFLINE},
            //{"ID_CARD_READER_NOT_CONNECT",EHDemoPublic.MINOR_ID_CARD_READER_NOT_CONNECT},
            //{"ID_CARD_READER_RESUME",EHDemoPublic.MINOR_ID_CARD_READER_RESUME},
            //{"FINGER_PRINT_MODULE_NOT_CONNECT",EHDemoPublic.MINOR_FINGER_PRINT_MODULE_NOT_CONNECT},
            //{"FINGER_PRINT_MODULE_RESUME",EHDemoPublic.MINOR_FINGER_PRINT_MODULE_RESUME},
            //{"CAMERA_NOT_CONNECT",EHDemoPublic.MINOR_CAMERA_NOT_CONNECT},
            //{"CAMERA_RESUME",EHDemoPublic.MINOR_CAMERA_RESUME},
            //{"COM_NOT_CONNECT",EHDemoPublic.MINOR_COM_NOT_CONNECT},
            //{"COM_RESUME",EHDemoPublic.MINOR_COM_RESUME},
            //{"DEVICE_NOT_AUTHORIZE",EHDemoPublic.MINOR_DEVICE_NOT_AUTHORIZE},
            //{"PEOPLE_AND_ID_CARD_DEVICE_ONLINE",EHDemoPublic.MINOR_PEOPLE_AND_ID_CARD_DEVICE_ONLINE},
            //{"PEOPLE_AND_ID_CARD_DEVICE_OFFLINE",EHDemoPublic.MINOR_PEOPLE_AND_ID_CARD_DEVICE_OFFLINE},
            //{"LOCAL_LOGIN_LOCK",EHDemoPublic.MINOR_LOCAL_LOGIN_LOCK},
            //{"LOCAL_LOGIN_UNLOCK",EHDemoPublic.MINOR_LOCAL_LOGIN_UNLOCK},
            //{"SUBMARINEBACK_COMM_BREAK",EHDemoPublic.MINOR_SUBMARINEBACK_COMM_BREAK},
            //{"SUBMARINEBACK_COMM_RESUME",EHDemoPublic.MINOR_SUBMARINEBACK_COMM_RESUME},
            //{"MOTOR_SENSOR_EXCEPTION",EHDemoPublic.MINOR_MOTOR_SENSOR_EXCEPTION},
            //{"CAN_BUS_EXCEPTION",EHDemoPublic.MINOR_CAN_BUS_EXCEPTION},
            //{"CAN_BUS_RESUME",EHDemoPublic.MINOR_CAN_BUS_RESUME},
            //{"GATE_TEMPERATURE_OVERRUN",EHDemoPublic.MINOR_GATE_TEMPERATURE_OVERRUN},
            //{"IR_EMITTER_EXCEPTION",EHDemoPublic.MINOR_IR_EMITTER_EXCEPTION},
            //{"IR_EMITTER_RESUME",EHDemoPublic.MINOR_IR_EMITTER_RESUME},
            //{"LAMP_BOARD_COMM_EXCEPTION",EHDemoPublic.MINOR_LAMP_BOARD_COMM_EXCEPTION},
            //{"LAMP_BOARD_COMM_RESUME",EHDemoPublic.MINOR_LAMP_BOARD_COMM_RESUME},
            //{"IR_ADAPTOR_COMM_EXCEPTION",EHDemoPublic.MINOR_IR_ADAPTOR_COMM_EXCEPTION},
            //{"IR_ADAPTOR_COMM_RESUME",EHDemoPublic.MINOR_IR_ADAPTOR_COMM_RESUME},
            //{"PRINTER_ONLINE",EHDemoPublic.MINOR_PRINTER_ONLINE},
            //{"PRINTER_OFFLINE",EHDemoPublic.MINOR_PRINTER_OFFLINE},
            //{"4G_MOUDLE_ONLINE",EHDemoPublic.MINOR_4G_MOUDLE_ONLINE},
            //{"4G_MOUDLE_OFFLINE",EHDemoPublic.MINOR_4G_MOUDLE_OFFLINE},
            //{"LOCAL_UPGRADE",EHDemoPublic.MINOR_LOCAL_UPGRADE},
            //{"REMOTE_LOGIN",EHDemoPublic.MINOR_REMOTE_LOGIN},
            //{"REMOTE_LOGOUT",EHDemoPublic.MINOR_REMOTE_LOGOUT},
            //{"MINOR_REMOTE_ARM",EHDemoPublic.MINOR_REMOTE_ARM},
            //{"REMOTE_DISARM",EHDemoPublic.MINOR_REMOTE_DISARM},
            //{"REMOTE_REBOOT",EHDemoPublic.MINOR_REMOTE_REBOOT},
            //{"REMOTE_UPGRADE",EHDemoPublic.MINOR_REMOTE_UPGRADE},
            //{"REMOTE_CFGFILE_OUTPUT",EHDemoPublic.MINOR_REMOTE_CFGFILE_OUTPUT},
            //{"REMOTE_CFGFILE_INTPUT",EHDemoPublic.MINOR_REMOTE_CFGFILE_INTPUT},
            //{"REMOTE_ALARMOUT_OPEN_MAN",EHDemoPublic.MINOR_REMOTE_ALARMOUT_OPEN_MAN},
            //{"REMOTE_ALARMOUT_CLOSE_MAN",EHDemoPublic.MINOR_REMOTE_ALARMOUT_CLOSE_MAN},
            //{"REMOTE_OPEN_DOOR",EHDemoPublic.MINOR_REMOTE_OPEN_DOOR},
            //{"REMOTE_CLOSE_DOOR",EHDemoPublic.MINOR_REMOTE_CLOSE_DOOR},
            //{"REMOTE_ALWAYS_OPEN",EHDemoPublic.MINOR_REMOTE_ALWAYS_OPEN},
            //{"REMOTE_ALWAYS_CLOSE",EHDemoPublic.MINOR_REMOTE_ALWAYS_CLOSE},
            //{"REMOTE_CHECK_TIME",EHDemoPublic.MINOR_REMOTE_CHECK_TIME},
            //{"NTP_CHECK_TIME",EHDemoPublic.MINOR_NTP_CHECK_TIME},
            //{"REMOTE_CLEAR_CARD",EHDemoPublic.MINOR_REMOTE_CLEAR_CARD},
            //{"REMOTE_RESTORE_CFG",EHDemoPublic.MINOR_REMOTE_RESTORE_CFG},
            //{"ALARMIN_ARM",EHDemoPublic.MINOR_ALARMIN_ARM},
            //{"ALARMIN_DISARM",EHDemoPublic.MINOR_ALARMIN_DISARM},
            //{"LOCAL_RESTORE_CFG",EHDemoPublic.MINOR_LOCAL_RESTORE_CFG},
            //{"REMOTE_CAPTURE_PIC",EHDemoPublic.MINOR_REMOTE_CAPTURE_PIC},
            //{"MOD_NET_REPORT_CFG",EHDemoPublic.MINOR_MOD_NET_REPORT_CFG},
            //{"MOD_GPRS_REPORT_PARAM",EHDemoPublic.MINOR_MOD_GPRS_REPORT_PARAM},
            //{"MOD_REPORT_GROUP_PARAM",EHDemoPublic.MINOR_MOD_REPORT_GROUP_PARAM},
            //{"UNLOCK_PASSWORD_OPEN_DOOR",EHDemoPublic.MINOR_UNLOCK_PASSWORD_OPEN_DOOR},
            //{"AUTO_RENUMBER",EHDemoPublic.MINOR_AUTO_RENUMBER},
            //{"AUTO_COMPLEMENT_NUMBER",EHDemoPublic.MINOR_AUTO_COMPLEMENT_NUMBER},
            //{"NORMAL_CFGFILE_INPUT",EHDemoPublic.MINOR_NORMAL_CFGFILE_INPUT},
            //{"NORMAL_CFGFILE_OUTTPUT",EHDemoPublic.MINOR_NORMAL_CFGFILE_OUTTPUT},
            //{"CARD_RIGHT_INPUT",EHDemoPublic.MINOR_CARD_RIGHT_INPUT},
            //{"CARD_RIGHT_OUTTPUT",EHDemoPublic.MINOR_CARD_RIGHT_OUTTPUT},
            //{"LOCAL_USB_UPGRADE",EHDemoPublic.MINOR_LOCAL_USB_UPGRADE},
            //{"REMOTE_VISITOR_CALL_LADDER",EHDemoPublic.MINOR_REMOTE_VISITOR_CALL_LADDER},
            //{"REMOTE_HOUSEHOLD_CALL_LADDER",EHDemoPublic.MINOR_REMOTE_HOUSEHOLD_CALL_LADDER},
            //{"REMOTE_ACTUAL_GUARD",EHDemoPublic.MINOR_REMOTE_ACTUAL_GUARD},
            //{"REMOTE_ACTUAL_UNGUARD",EHDemoPublic.MINOR_REMOTE_ACTUAL_UNGUARD},
            //{"REMOTE_CONTROL_NOT_CODE_OPER_FAILED",EHDemoPublic.MINOR_REMOTE_CONTROL_NOT_CODE_OPER_FAILED},
            //{"REMOTE_CONTROL_CLOSE_DOOR",EHDemoPublic.MINOR_REMOTE_CONTROL_CLOSE_DOOR},
            //{"REMOTE_CONTROL_OPEN_DOOR",EHDemoPublic.MINOR_REMOTE_CONTROL_OPEN_DOOR},
            //{"REMOTE_CONTROL_ALWAYS_OPEN_DOOR",EHDemoPublic.MINOR_REMOTE_CONTROL_ALWAYS_OPEN_DOOR},
            //{"LEGAL_CARD_PASS",EHDemoPublic.MINOR_LEGAL_CARD_PASS},
            //{"CARD_AND_PSW_PASS",EHDemoPublic.MINOR_CARD_AND_PSW_PASS},
            //{"CARD_AND_PSW_FAIL",EHDemoPublic.MINOR_CARD_AND_PSW_FAIL},
            //{"CARD_AND_PSW_TIMEOUT",EHDemoPublic.MINOR_CARD_AND_PSW_TIMEOUT},
            //{"CARD_AND_PSW_OVER_TIME",EHDemoPublic.MINOR_CARD_AND_PSW_OVER_TIME},
            //{"CARD_NO_RIGHT",EHDemoPublic.MINOR_CARD_NO_RIGHT},
            //{"CARD_INVALID_PERIOD",EHDemoPublic.MINOR_CARD_INVALID_PERIOD},
            //{"CARD_OUT_OF_DATE",EHDemoPublic.MINOR_CARD_OUT_OF_DATE},
            //{"INVALID_CARD",EHDemoPublic.MINOR_INVALID_CARD},
            //{"ANTI_SNEAK_FAIL",EHDemoPublic.MINOR_ANTI_SNEAK_FAIL},
            //{"INTERLOCK_DOOR_NOT_CLOSE",EHDemoPublic.MINOR_INTERLOCK_DOOR_NOT_CLOSE},
            //{"NOT_BELONG_MULTI_GROUP",EHDemoPublic.MINOR_NOT_BELONG_MULTI_GROUP},
            //{"INVALID_MULTI_VERIFY_PERIOD",EHDemoPublic.MINOR_INVALID_MULTI_VERIFY_PERIOD},
            //{"MULTI_VERIFY_SUPER_RIGHT_FAIL",EHDemoPublic.MINOR_MULTI_VERIFY_SUPER_RIGHT_FAIL},
            //{"MULTI_VERIFY_REMOTE_RIGHT_FAIL",EHDemoPublic.MINOR_MULTI_VERIFY_REMOTE_RIGHT_FAIL},
            //{"MULTI_VERIFY_SUCCESS",EHDemoPublic.MINOR_MULTI_VERIFY_SUCCESS},
            //{"LEADER_CARD_OPEN_BEGIN",EHDemoPublic.MINOR_LEADER_CARD_OPEN_BEGIN},
            //{"LEADER_CARD_OPEN_END",EHDemoPublic.MINOR_LEADER_CARD_OPEN_END},
            //{"ALWAYS_OPEN_BEGIN",EHDemoPublic.MINOR_ALWAYS_OPEN_BEGIN},
            //{"ALWAYS_OPEN_END",EHDemoPublic.MINOR_ALWAYS_OPEN_END},
            //{"LOCK_OPEN",EHDemoPublic.MINOR_LOCK_OPEN},
            //{"LOCK_CLOSE",EHDemoPublic.MINOR_LOCK_CLOSE},
            //{"DOOR_BUTTON_PRESS",EHDemoPublic.MINOR_DOOR_BUTTON_PRESS},
            //{"DOOR_BUTTON_RELEASE",EHDemoPublic.MINOR_DOOR_BUTTON_RELEASE},
            //{"DOOR_OPEN_NORMAL",EHDemoPublic.MINOR_DOOR_OPEN_NORMAL},
            //{"DOOR_CLOSE_NORMAL",EHDemoPublic.MINOR_DOOR_CLOSE_NORMAL},
            //{"DOOR_OPEN_ABNORMAL",EHDemoPublic.MINOR_DOOR_OPEN_ABNORMAL},
            //{"DOOR_OPEN_TIMEOUT",EHDemoPublic.MINOR_DOOR_OPEN_TIMEOUT},
            //{"ALARMOUT_ON",EHDemoPublic.MINOR_ALARMOUT_ON},
            //{"ALARMOUT_OFF",EHDemoPublic.MINOR_ALARMOUT_OFF},
            //{"ALWAYS_CLOSE_BEGIN",EHDemoPublic.MINOR_ALWAYS_CLOSE_BEGIN},
            //{"ALWAYS_CLOSE_END",EHDemoPublic.MINOR_ALWAYS_CLOSE_END},
            //{"MULTI_VERIFY_NEED_REMOTE_OPEN",EHDemoPublic.MINOR_MULTI_VERIFY_NEED_REMOTE_OPEN},
            //{"MULTI_VERIFY_SUPERPASSWD_VERIFY_SUCCESS",EHDemoPublic.MINOR_MULTI_VERIFY_SUPERPASSWD_VERIFY_SUCCESS},
            //{"MULTI_VERIFY_REPEAT_VERIFY",EHDemoPublic.MINOR_MULTI_VERIFY_REPEAT_VERIFY},
            //{"MULTI_VERIFY_TIMEOUT",EHDemoPublic.MINOR_MULTI_VERIFY_TIMEOUT},
            //{"DOORBELL_RINGING",EHDemoPublic.MINOR_DOORBELL_RINGING},
            //{"FINGERPRINT_COMPARE_PASS",EHDemoPublic.MINOR_FINGERPRINT_COMPARE_PASS},
            //{"FINGERPRINT_COMPARE_FAIL",EHDemoPublic.MINOR_FINGERPRINT_COMPARE_FAIL},
            //{"CARD_FINGERPRINT_VERIFY_PASS",EHDemoPublic.MINOR_CARD_FINGERPRINT_VERIFY_PASS},
            //{"CARD_FINGERPRINT_VERIFY_FAIL",EHDemoPublic.MINOR_CARD_FINGERPRINT_VERIFY_FAIL},
            //{"CARD_FINGERPRINT_VERIFY_TIMEOUT",EHDemoPublic.MINOR_CARD_FINGERPRINT_VERIFY_TIMEOUT},
            //{"CARD_FINGERPRINT_PASSWD_VERIFY_PASS",EHDemoPublic.MINOR_CARD_FINGERPRINT_PASSWD_VERIFY_PASS},
            //{"CARD_FINGERPRINT_PASSWD_VERIFY_FAIL",EHDemoPublic.MINOR_CARD_FINGERPRINT_PASSWD_VERIFY_FAIL},
            //{"CARD_FINGERPRINT_PASSWD_VERIFY_TIMEOUT",EHDemoPublic.MINOR_CARD_FINGERPRINT_PASSWD_VERIFY_TIMEOUT},
            //{"FINGERPRINT_PASSWD_VERIFY_PASS",EHDemoPublic.MINOR_FINGERPRINT_PASSWD_VERIFY_PASS},
            //{"FINGERPRINT_PASSWD_VERIFY_FAIL",EHDemoPublic.MINOR_FINGERPRINT_PASSWD_VERIFY_FAIL},
            //{"FINGERPRINT_PASSWD_VERIFY_TIMEOUT",EHDemoPublic.MINOR_FINGERPRINT_PASSWD_VERIFY_TIMEOUT},
            //{"FINGERPRINT_INEXISTENCE",EHDemoPublic.MINOR_FINGERPRINT_INEXISTENCE},
            //{"CARD_PLATFORM_VERIFY",EHDemoPublic.MINOR_CARD_PLATFORM_VERIFY},
            //{"MAC_DETECT",EHDemoPublic.MINOR_MAC_DETECT},
            //{"LEGAL_MESSAGE",EHDemoPublic.MINOR_LEGAL_MESSAGE},
            //{"ILLEGAL_MESSAGE",EHDemoPublic.MINOR_ILLEGAL_MESSAGE},
            //{"DOOR_OPEN_OR_DORMANT_FAIL",EHDemoPublic.MINOR_DOOR_OPEN_OR_DORMANT_FAIL},
            //{"AUTH_PLAN_DORMANT_FAIL",EHDemoPublic.MINOR_AUTH_PLAN_DORMANT_FAIL},
            //{"CARD_ENCRYPT_VERIFY_FAIL",EHDemoPublic.MINOR_CARD_ENCRYPT_VERIFY_FAIL},
            //{"SUBMARINEBACK_REPLY_FAIL",EHDemoPublic.MINOR_SUBMARINEBACK_REPLY_FAIL},
            //{"DOOR_OPEN_OR_DORMANT_OPEN_FAIL",EHDemoPublic.MINOR_DOOR_OPEN_OR_DORMANT_OPEN_FAIL},
            //{"DOOR_OPEN_OR_DORMANT_LINKAGE_OPEN_FAIL",EHDemoPublic.MINOR_DOOR_OPEN_OR_DORMANT_LINKAGE_OPEN_FAIL},
            //{"TRAILING",EHDemoPublic.MINOR_TRAILING},
            //{"REVERSE_ACCESS",EHDemoPublic.MINOR_REVERSE_ACCESS},
            //{"FORCE_ACCESS",EHDemoPublic.MINOR_FORCE_ACCESS},
            //{"CLIMBING_OVER_GATE",EHDemoPublic.MINOR_CLIMBING_OVER_GATE},
            //{"PASSING_TIMEOUT",EHDemoPublic.MINOR_PASSING_TIMEOUT},
            //{"INTRUSION_ALARM",EHDemoPublic.MINOR_INTRUSION_ALARM},
            //{"FREE_GATE_PASS_NOT_AUTH",EHDemoPublic.MINOR_FREE_GATE_PASS_NOT_AUTH},
            //{"DROP_ARM_BLOCK",EHDemoPublic.MINOR_DROP_ARM_BLOCK},
            //{"DROP_ARM_BLOCK_RESUME",EHDemoPublic.MINOR_DROP_ARM_BLOCK_RESUME},
            //{"LOCAL_FACE_MODELING_FAIL",EHDemoPublic.MINOR_LOCAL_FACE_MODELING_FAIL},
            //{"STAY_EVENT",EHDemoPublic.MINOR_STAY_EVENT},
            //{"PASSWORD_MISMATCH",EHDemoPublic.MINOR_PASSWORD_MISMATCH},
            //{"EMPLOYEE_NO_NOT_EXIST",EHDemoPublic.MINOR_EMPLOYEE_NO_NOT_EXIST},
            //{"COMBINED_VERIFY_PASS",EHDemoPublic.MINOR_COMBINED_VERIFY_PASS},
            //{"COMBINED_VERIFY_TIMEOUT",EHDemoPublic.MINOR_COMBINED_VERIFY_TIMEOUT},
            //{"VERIFY_MODE_MISMATCH",EHDemoPublic.MINOR_VERIFY_MODE_MISMATCH}
        };

    private static Dictionary<string, uint> MajorTypeDictionary = new Dictionary<string, uint>()
        {
            {"All",0},
            {"Alarm",1},
            {"Exception",2},
            {"Operation",3},
            {"Event",5}
        };

    private static Dictionary<string, ushort> ComInductiveEvent = new Dictionary<string, ushort>()
        {
            {"invalid",0},
            {"authenticated",1},
            {"authenticationFailed",2},
            {"openingDoor",3},
            {"closingDoor",4},
            {"doorException",5},
            {"remoteOperation",6},
            {"timeSynchronization",7},
            {"deviceException",8},
            {"deviceRecovered",9},
            {"alarmTriggered",10},
            {"alarmRecovered",11},
            {"callCenter",12},
            {"all",0xffff}
        };

    public static int NumOfInductiveEvent()
    {
        return ComInductiveEvent.Count;
    }

    public static string FindKeyOfInductive(ushort value)
    {
        string res = "";
        foreach (var item in ComInductiveEvent)
        {
            if (item.Value == value)
            {
                res = item.Key;
            }
        }
        return res;
    }
    private static Dictionary<string, int> SearchType = new Dictionary<string, int>()
        {
            {"Invalid",0},
            {"Event source",1},
            {"Monitor ID",2}
        };
}