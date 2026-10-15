using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class PostCard
    {

        /// <summary>
        /// 清空所有卡参数
        /// </summary>
        /// <param name="lUserID"></param>
        /// <returns></returns>
        public static bool ClearAll(int lUserID)
        {
            HCISUPCMS.NET_EHOME_XML_REMOTE_CTRL_PARAM pInputCtrlXml = new HCISUPCMS.NET_EHOME_XML_REMOTE_CTRL_PARAM();
            int nInSize = Marshal.SizeOf(pInputCtrlXml);
            pInputCtrlXml.dwSize = (uint)nInSize;
            pInputCtrlXml.dwSendTimeOut = 30 * 1000;
            pInputCtrlXml.dwRecvTimeOut = 30 * 1000;
            pInputCtrlXml.dwInBufferSize = 1500;
            pInputCtrlXml.dwOutBufferSize = 102400;
            pInputCtrlXml.lpInbuffer = Marshal.AllocHGlobal(1500);
            pInputCtrlXml.lpOutBuffer = Marshal.AllocHGlobal(1024 * 100);

            string strInputParam = @"<?xml version=""1.0"" encoding=""GB2312"" ?>
<PPVSPMessage>
<Version>2.5</Version>
<Sequence>1</Sequence>
<Method>CONTROL</Method>
<CommandType>REQUEST</CommandType>
<Command>CLEARACSPARAM</Command>
<Params>
<paramType>13</paramType>
</Params>
</PPVSPMessage>";//需要清空的参数类型,1-门状态周计划，
                 //2- 读卡器周计划，3 - 卡权限周计划，4 - 门状态假日计划，5 - 读卡器假日计划，
                 //6 - 卡权限假日计划，7 - 门状态假日组，8 - 读卡器验证方式假日组，9 - 卡权限
                 //假日组，10 - 门状态计划模板，11 - 读卡器验证方式计划模板，12 - 卡权限计划
                 //模板，13 - 卡参数，14 - 群组参数，15 - 反潜回参数,16 - 所有参数，17 - 事件卡号
                 //联动参数，18 - 密码开门使能参数，19 - 多门互锁参数，20 - 多重认证， 21 - 黑
                 //名单图片参数，22 - 身份证黑名单参数
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCtrlXml.lpInbuffer = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCtrlXml.lpInbuffer, byInputLen);
            pInputCtrlXml.dwInBufferSize = (uint)byInputLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCtrlXml));
            Marshal.StructureToPtr(pInputCtrlXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLRemoteControl(lUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                string strErr = "删除所有卡参数失败" + iLastErr;
                Marshal.FreeHGlobal(pInputCtrlXml.lpInbuffer);
                return false;
            }
            Marshal.FreeHGlobal(pInputCtrlXml.lpInbuffer);
            return true;
        }
    }
}
