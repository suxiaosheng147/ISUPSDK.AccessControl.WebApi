using ISUPSDK.AccessControl.WebApi.Services.Logging;
using HKEDUBasicDataSync.Methods;
using Newtonsoft.Json;
using System;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class ISUPResult
    {
        /// <summary>
        /// /*ro, opt, int, 状态码, range:[,], step:, unit:, unitType:, desc:无法用1表示时（1表示成功且无特殊状态）必须返回*/
        /// </summary>
        public int statusCode { get; set; }

        /// <summary>
        /// /*ro, opt, string, 状态描述, range:[1,64], desc:无法用ok表示时（ok表示成功且无特殊状态）必须返回*/
        /// </summary>
        public string statusString { get; set; } = "";

        /// <summary>
        ///  /*ro, opt, string, 子状态码, range:[1,64], desc:无法用ok表示（ok表示成功且无特殊状态）必须返回*/
        /// </summary>
        public string subStatusCode { get; set; } = "";


        /// <summary>
        /// /*ro, opt, int, 错误码, range:[,], step:, unit:, unitType:, desc:当statusCode不为1时，与subStatusCode对应*/
        /// </summary>
        public int errorCode { get; set; }

        /// <summary>
        /// /*ro, opt, int, 错误码, range:[,], step:, unit:, unitType:, desc:当statusCode不为1时，与subStatusCode对应*/
        /// </summary>
        public string errorMsg { get; set; } = "";
    }
    public class ResponseStatus
    {
        public string requestURL { get; set; }
        public int statusCode { get; set; }
        public string statusString { get; set; }
        public string subStatusCode { get; set; }
        public int errorCode { get; set; }
        public string errorMsg { get; set; }
    }

    public class ISUPResultMethod
    {
        public static bool Method(string? iSUPResultStr, out int statusCode, out string errorMsg)
        {
            errorMsg = "";
            bool lb = false;
            statusCode = -1;

            if (string.IsNullOrWhiteSpace(iSUPResultStr))
            {
                return lb;
            }
            try
            {
                ResponseStatus? iSUPResult = JsonConvert.DeserializeObject<ResponseStatus>(iSUPResultStr);

                if (iSUPResult == null)
                {
                    return false;
                }

                if (1 == iSUPResult.statusCode)
                {
                    statusCode = iSUPResult.statusCode;
                    lb = true;
                }

                IsupLog.Info(JsonHepler.ToJson(iSUPResult));
                errorMsg = iSUPResult.statusString + errorMsg;
                return lb;
            }
            catch (Exception)
            {

                return lb;
            }
        }

        public static bool Method(string? iSUPResultStr, out int statusCode, out string errorMsg,out string json)
        {
            errorMsg = "";
            bool lb = false;
            statusCode = -1;
            json=string.Empty;
            if (string.IsNullOrWhiteSpace(iSUPResultStr))
            {
                return lb;
            }
            try
            {
                ResponseStatus? iSUPResult = JsonConvert.DeserializeObject<ResponseStatus>(iSUPResultStr);

                if (iSUPResult == null)
                {
                    return false;
                }
                json=JsonHepler.ToJson(iSUPResult);
                if (1 == iSUPResult.statusCode)
                {
                    statusCode = iSUPResult.statusCode;
                    lb = true;
                }


                errorMsg = iSUPResult.statusString + errorMsg;
                return lb;
            }
            catch (Exception)
            {

                return lb;
            }
        }


        public static bool ISUPResult(string? iSUPResultStr, out int statusCode, out string errorMsg)
        {
            errorMsg = "";
            bool lb = false;
            statusCode = -1;

            if (string.IsNullOrWhiteSpace(iSUPResultStr))
            {
                return lb;
            }
            try
            {
                ISUPResult? iSUPResult = JsonConvert.DeserializeObject<ISUPResult>(iSUPResultStr);

                if (iSUPResult == null)
                {
                    return false;
                }

                if (1 == iSUPResult.statusCode)
                {
                    statusCode = iSUPResult.statusCode;
                    lb = true;
                }


                errorMsg = iSUPResult.statusString + errorMsg;
                return lb;
            }
            catch (Exception)
            {

                return lb;
            }
        }
    }
}
