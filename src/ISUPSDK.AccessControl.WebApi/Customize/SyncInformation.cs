using ISUPSDK.AccessControl.WebApi.Services.Logging;
using Experimental.System.Messaging;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Distribute;
using ISUPSDK.AccessControl.WebApi.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using System;
using System.Collections.Specialized;
using System.Text;
using System.Web;


namespace ISUPSDK.AccessControl.WebApi.Customize
{
    /// <summary>
    /// 同步资料
    /// </summary>
    public class SyncInformation
    {
        


        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="Prefix"></param>
        /// <param name="person"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        public static async Task<(bool lb, List<returnErrorModel> tempIdList)> AddPerson(im_machine machine, string Prefix, Detail person, UserInfo? userInfo, Settings settings)
        {
            var tempIdList = new List<returnErrorModel>();
            bool lb = false;
            try
            {
                bool isSuccess = false;

                // 第一步：尝试添加用户基本信息
                //判断人员的基础数据一致，就不下发数据
                var ret1 = false;
                //if (!InspectBasicsData(person, userInfo))
                //{
                //    ret1 = ISUPPostPerson.Post(machine, person, Prefix, out tempIdList);
                //    isSuccess = ret1;
                //}
                //else
                //{
                //    ret1 = true;
                //    isSuccess = true;
                //}
                ret1 = ISUPPostPerson.Post(machine, person, Prefix, out tempIdList);
                isSuccess = ret1;
                // 第二步：如果有卡ID并且机器支持读卡，添加卡信息
                bool ret2 = false;
                if (!string.IsNullOrWhiteSpace(person.cardId) && ret1)
                {
                    ret2 = ISUPPostCard.Post(machine, false, person, Prefix, out tempIdList);
                    isSuccess = ret2;
                }
                // 第三步：如果需要更新照片，并且成功添加基本信息，则添加人脸信息
                bool ret3 = false;
                if (ret1 && person.photoPath != "" && person.updatePhoto)
                {
                    var per =await ISUPPostFace.PUT(machine, person, Prefix, settings);
                    ret3 = per.lb;
                    tempIdList = per.tempIdList;
                    isSuccess = ret3;
                }
                else if(ret1 &&string.IsNullOrWhiteSpace(person.photoPath))
                {
                    //检查是否有人脸,如果有人脸删除掉
                    ISUPPostFace.DelFace(machine, Prefix + person.webId);

                }
                // 更新tempIdList并返回成功信息
                if (isSuccess)
                {
                    // 移除旧的错误信息
                    tempIdList.RemoveAll(w => w.DataId == person.dataId);

                    // 根据不同的条件添加成功信息
                    if (person.updatePhoto == true && ret3 == true) // 照片更新成功
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "新增人员信息成功", ErrCode = 0 });
                    }
                    else if (person.updatePhoto == true && (ret1 && ret3)) // 基本信息和照片更新成功
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "新增人员信息成功", ErrCode = 0 });
                    }
                    else if (person.updatePhoto == false && !string.IsNullOrEmpty(person.cardId) && ret1 && !ret3) // 只有卡下发
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "新增人员信息成功", ErrCode = 0 });
                    }
                    else if (!string.IsNullOrEmpty(person.cardId) && ret1 && ret2) // 只有卡下发
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "新增人员信息成功", ErrCode = 0 });
                    }
                    else if (ret1 && !person.updatePhoto && string.IsNullOrEmpty(person.cardId)) // 基本信息  无卡，无图
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "新增人员信息成功", ErrCode = 0 });
                    }
                }
                return (isSuccess, tempIdList);
            }
            catch (Exception)
            {
                return (lb, tempIdList);
            }
        }


        /// <summary>
        /// 修改用户
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="Prefix"></param>
        /// <param name="person"></param>
        /// <param name="tempIdList"></param>
        /// <returns></returns>
        public static bool UpdatePerson(im_machine machine, string Prefix, Detail person, UserInfo? userInfo, Settings settings, ref List<returnErrorModel> tempIdList)
        {

            try
            {
                bool isSuccess = false;
                // 第一步：尝试添加用户基本信息
                //判断人员的基础数据一致，就不下发数据
                var ret1 = false;
                //if (!InspectBasicsData(person, userInfo))
                //{
                //    ret1 = ISUPPostPerson.Post(machine, person, Prefix, out tempIdList);
                //    isSuccess = ret1;
                //}
                //else
                //{
                //    ret1 = true;
                //    isSuccess = true;
                //}


                
                ret1 = ISUPPostPerson.Post(machine, person, Prefix, out tempIdList);
                isSuccess = ret1;
                // 第二步：如果有卡ID并且机器支持读卡，添加卡信息
                bool ret2 = false;

                if (!string.IsNullOrWhiteSpace(person.oldCardId) && ret1)
                {
                    ISUPPostCard.Post(machine, true, person, Prefix, out tempIdList);
                    
                }


                if (!string.IsNullOrWhiteSpace(person.cardId) && ret1)
                {
                    ret2 = ISUPPostCard.Post(machine, false, person, Prefix, out tempIdList);
                    isSuccess = ret2;
                }



                // 第三步：如果需要更新照片，并且成功添加基本信息，则添加人脸信息
                bool ret3 = false;
                if (ret1 && person.photoPath != "" && person.updatePhoto)
                {
                    var per = ISUPPostFace.PUT(machine, person, Prefix, settings);
                    ret3 = per.Result.lb;
                    tempIdList = per.Result.tempIdList;
                    isSuccess = ret3;
                }
                else if (ret1 && string.IsNullOrWhiteSpace(person.photoPath))
                {
                    //检查是否有人脸,如果有人脸删除掉
                    ISUPPostFace.DelFace(machine, Prefix + person.webId);

                }

                // 更新tempIdList并返回成功信息
                if (isSuccess)
                {
                    // 移除旧的错误信息
                    tempIdList.RemoveAll(w => w.DataId == person.dataId);

                    // 根据不同的条件添加成功信息
                    if (person.updatePhoto == true && ret3 == true) // 照片更新成功
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "设置人员信息成功", ErrCode = 0 });
                    }
                    else if (person.updatePhoto == true && (ret1 && ret3)) // 基本信息和照片更新成功
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "设置人员信息成功", ErrCode = 0 });
                    }
                    else if (person.updatePhoto == false && !string.IsNullOrEmpty(person.cardId) && ret1 && !ret3) // 只有卡下发
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "设置人员信息成功", ErrCode = 0 });
                    }
                    else if (!string.IsNullOrEmpty(person.cardId) && ret1 && ret2) // 只有卡下发
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "设置人员信息成功", ErrCode = 0 });
                    }
                    else if (ret1 && !person.updatePhoto && string.IsNullOrEmpty(person.cardId)) // 基本信息  无卡，无图
                    {
                        tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "设置人员信息成功", ErrCode = 0 });
                    }
                }


                return isSuccess;
            }
            catch (Exception)
            {

                return false;
            }
        }


        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="Prefix"></param>
        /// <param name="person"></param>
        /// <param name="tempIdList"></param>
        /// <returns></returns>
        public static bool DeletePerson(im_machine machine, string Prefix, Detail person, ref List<returnErrorModel> tempIdList)
        {
            try
            {
                //var ret = ISUPPostPerson.Del1(machine, person, Prefix, ref tempIdList);

                var ret = ISUPPostPerson.DelPerson(machine, Prefix, person.webId, 30000, 500);
                ISUPPostFace.DelFace(machine, Prefix + person.webId);

                tempIdList.Add(new returnErrorModel { DataId = person.dataId, Err = "删除人员信息"+ ret.Item2, ErrCode = 0 });
                return ret.Item1;
            }
            catch (Exception)
            {

                return false;
            }
        }

        /// <summary>
        /// 检查基础数据是否一致
        /// </summary>
        /// <param name="person"></param>
        /// <param name="userInfo"></param>
        /// <returns></returns>
        private static bool InspectBasicsData(Detail person, UserInfo? userInfo)
        {
            bool result = false;

            try
            {
                if (userInfo == null)
                {
                    return result;
                }

                if (person.name == userInfo.Name)
                {
                    result = true;
                }

                if (userInfo.RightPlan != null && userInfo.RightPlan.Count > 0)
                {
                    foreach (var item in userInfo.RightPlan)
                    {
                        if (item.PlanTemplateNo == person.door.ToString())
                        {
                            result = true;
                        }
                    }
                }

                return result;
            }
            catch (Exception)
            {

                return false;
            }
        }



       /// <summary>
       /// 
       /// </summary>
       /// <param name="machine"></param>
       /// <param name="settings"></param>
       /// <returns></returns>
        public static async Task SyncDevicesAsync(im_machine machine, Settings settings)
        {
           
            try
            {
                string authorizationToken =Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.UpdateInfoUrl, machine.MachineSN);
                string urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<tv_settings>>(urlDB);
                    if (UpdateSetting == null)
                    {
                        return;
                    }
                    if (UpdateSetting.Status && UpdateSetting.Data != null)
                    {
                        if (UpdateSetting.Data.needInit)
                            if (!await AllInit(machine, settings)) // 异步调用 AllInit
                                return;
                        await SetAcsEvent(machine, settings);
                        if (UpdateSetting.Data.childCount > 0)
                        {

                            await SortOperationAsync("01", settings.StudentUpdateDataUrl, 3, machine, settings); // 异步调用 SortOperation
                        }

                        if (UpdateSetting.Data.parentCount > 0)
                        {
                            await SortOperationAsync("02", settings.ParentUpdateDataUrl, 4, machine, settings); // 异步调用 SortOperation

                        }

                        if (UpdateSetting.Data.staffCount > 0)
                        {
                            await SortOperationAsync("03", settings.StaffUpdateDataUrl, 8, machine, settings); // 异步调用 SortOperation
                        }
                        if (!UpdateSetting.Data.needInit)
                        {
                            if (UpdateSetting.Data.doorCount > 0)
                            {
                                await SyncDoorData(machine, settings);
                            }
                        }

                        if (UpdateSetting.Data.LeaveSyncCount > 0 && !UpdateSetting.Data.needInit)
                        {
                            await SetLeaveSyncAsync(machine, settings);
                        }

                        if (UpdateSetting.Data.LeaveResetSyncCount > 0 && !UpdateSetting.Data.needInit)
                        {
                            await SetLeaveResetSyncAsync(machine, settings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Common.WriteFileHours("log.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
            }
        }



        /// <summary>
        /// 全部初始化
        /// </summary>
        /// <param name="machine"></param>
        /// <returns></returns>

        private static async Task<bool> AllInit(im_machine machine, Settings settings)
        {
            if (!PostCard.ClearAll(machine.lLoginID))
                return false;
            for (var i = 1; i < 15; i++)
            {
                PostDoor.SetTemplate(machine.lLoginID, i, i);
                PostDoor.Start(machine.lLoginID, i);
                var doorData = new DoorData { groupID = i, fromTime1 = "00:00:00", toTime1 = "23:59:10" };
                PostDoor.SetTime(machine.lLoginID, doorData);
            }


            #region 全天候不通行模板
            PostDoor.SetTemplate(machine.lLoginID, 99, 99);
            PostDoor.Start(machine.lLoginID, 99);
            var doorData1 = new DoorData { groupID = 99, fromTime1 = "00:00:00", toTime1 = "00:01:00" };
            PostDoor.SetTime(machine.lLoginID, doorData1);
            #endregion

            #region 全天候通行模板 
            PostDoor.SetTemplate(machine.lLoginID, 98, 98);
            PostDoor.Start(machine.lLoginID, 98);
            var doorData2 = new DoorData { groupID = 98, fromTime1 = "00:00:00", toTime1 = "23:59:59" };
            PostDoor.SetTime(machine.lLoginID, doorData2);
            #endregion


            string authorizationToken = Common.SetToken(settings.WebCheckCode1);
            var url = Common.GetUrl(settings.ReturnInitUrl, machine.MachineSN);
            string urlDB = await HttpHelper.PutUrlAsync(url,null, authorizationToken); // 异步调用 PostUrl

            return true;
        }


        /// <summary>
        /// 分类别更新人员信息操作
        /// </summary>
        /// <param name="Prefix"></param>
        /// <param name="Url"></param>
        /// <param name="ReturnDataType"></param>
        /// <param name="machine"></param>
        /// <returns></returns>
        private static async Task SortOperationAsync(string Prefix, string Url, int ReturnDataType, im_machine machine, Settings settings)
        {
            var successIdList = new List<string>();//成功ID列表
            var failIdList = new List<returnErrorModel>();//失败ID列表
            var tempIdList = new List<returnErrorModel>();//临时ID列表，当有成功的更新才进行结算
            string authorizationToken = Common.SetToken(settings.WebCheckCode1);
            var url = Common.GetUrl(Url, machine.MachineSN, settings.RecordNumber); //每次下发条数
            var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl
            if (!string.IsNullOrWhiteSpace(urlDB))
            {
                var UpdateData = JsonHepler.FromJson<UpdateData<List<Detail>>>(urlDB);
                if (UpdateData != null && UpdateData.Status)
                {

                    var delRet = false;
                    var addRet = false;

                    var putRet = false;
                    string tempList = string.Empty;

                    var employeeNos = UpdateData.Data.Select(w => Prefix + w.webId).ToList();
                    //ISUPPostPerson.GetUserInfoSearch(machine, employeeNos, settings.RecordNumber, out List<UserInfo> userInfoList); // 异步调用 GetUserInfoSearch


                    var addList = UpdateData.Data.Where(w => w.method == 1).ToList();

                    var putList = UpdateData.Data.Where(w => w.method == 2).ToList();

                    var delList = UpdateData.Data.Where(w => w.method == 3).ToList();

                    #region  删除
                    foreach (var item in delList)
                    {
                        delRet = DeletePerson(machine, Prefix, item, ref tempIdList);

                        if (delRet)
                        {
                            successIdList.Add(item.dataId.ToString());
                        }
                        else
                        {
                            failIdList.AddRange(tempIdList);
                        }

                    }
                    #endregion


                    #region 添加

                    foreach (var item in addList)
                    {
                        // 获取这一批已经存在的用户
                        UserInfo userInfo = new(); //userInfoList.FirstOrDefault(w => w.EmployeeNo == Prefix + item.webId);
                        
                        var s = await AddPerson(machine, Prefix, item, userInfo, settings);
                        addRet =s.lb;
                        tempIdList = s.tempIdList;
                        if (addRet)
                        {
                            successIdList.Add(item.dataId.ToString());
                        }
                        else
                        {
                            failIdList.AddRange(tempIdList);
                        }
                    }

                    #endregion


                    #region 更新
                    foreach (var item in putList)
                    {
                        // 获取这一批已经存在的用户
                        //var userInfo = userInfoList.FirstOrDefault(w => w.EmployeeNo == Prefix + item.webId);
                        UserInfo userInfo = new();
                        putRet = UpdatePerson(machine, Prefix, item, userInfo, settings, ref tempIdList);

                        if (putRet)
                        {
                            successIdList.Add(item.dataId.ToString());
                        }
                        else
                        {
                            failIdList.AddRange(tempIdList);
                        }
                    }


                    #endregion


                    if (successIdList.Count > 0)
                    {
                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        tempList = string.Join(",", successIdList);
                        url = Common.GetReturnUrl(settings.ReturnDataUrl, machine.MachineSN, tempList, ReturnDataType);
                        
                        await HttpHelper.PutUrlAsync(url,null, authorizationToken); // 异步调用 WebPost
                    }
                    if (failIdList.Count > 0)
                    {
                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        tempList = string.Empty;
                        url = Common.GetReturnUrl(settings.ReturnErrorUrl, machine.MachineSN, null, ReturnDataType);
                        await HttpHelper.PutUrlAsync(url, failIdList, authorizationToken); // 异步调用 PostUrl
                    }

                    await Task.Delay(5000);
                }
            }
        }


        /// <summary>
        /// 更新门禁信息
        /// </summary>
        /// <param name="machine"></param>
        private static async Task SyncDoorData1(im_machine machine, Settings settings)
        {
            try
            {
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.DoorUpdateDataUrl, machine.MachineSN, 1000);
                string urlDB = await HttpHelper.GetUrlAsync(url,authorizationToken);
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<List<DoorData>>>(urlDB);

                    if (UpdateSetting != null)
                    {
                        if (UpdateSetting.Status && (UpdateSetting.Data != null && UpdateSetting.Data.Count > 0))
                        {

                            List<int> successDataIds = [];
                            List<ErrInfo> FailureData=[];
                            foreach (var item in UpdateSetting.Data)
                            {
                                if (item.method == 1 || item.method == 2)
                                {
                                    //判断这个门禁配置了假日计划，先生成假日，然后把这个加入到这个假日组中
                                    string holidayGroupNo = "";
                                    

                                    foreach (var item_HolidayGroupData in item.HolidayGroupDatas)
                                    {
                                        string holidayPeriodNo = "";
                                        string strOutPuts = "";
                                        foreach (var item_HolidayPeriod in item_HolidayGroupData.HolidayPeriodData)
                                        {
                                            await Task.Delay(5000);
                                            //配置假日计划
                                            if (!PostCardHolidayPlan.SetCardHolidayPlan(machine.lLoginID, item_HolidayPeriod,out strOutPuts))
                                            {
                                                FailureData.Add(new ErrInfo() {  DataId= item.dataId , Err= strOutPuts });
                                                return;
                                            }
                                            //获取假日计划编号
                                            holidayPeriodNo += "," + item_HolidayPeriod.HolidayPlanNo;
                                        }
                                        holidayPeriodNo = holidayPeriodNo.Substring(1);

                                        if (!PostCardHolidayPlan.SetCardHolidayGroup(machine.lLoginID, item_HolidayGroupData.HolidayGroupNo, item_HolidayGroupData.GroupName, holidayPeriodNo, out strOutPuts))
                                        {
                                            FailureData.Add(new ErrInfo() { DataId = item.dataId, Err = strOutPuts });
                                            return;
                                        }
                                        else
                                        {
                                            holidayGroupNo += "," + item_HolidayGroupData.HolidayGroupNo;
                                        }
                                    }
                                    if (!string.IsNullOrWhiteSpace(holidayGroupNo))
                                    {
                                        holidayGroupNo = holidayGroupNo.Substring(1);
                                    }
                                    else
                                    {
                                        holidayGroupNo = "0";
                                    }

                                    var ret = PostDoor.SetTemplate(machine.lLoginID, item.groupID, item.groupID, holidayGroupNo) && PostDoor.SetTime(machine.lLoginID, item);
                                    if (!ret)
                                    {
                                        FailureData.Add(new ErrInfo() { DataId= item.dataId, Err= "设置计划模板或者设置计划时间  失败" });
                                        return;
                                    }
                                    else
                                    {
                                        successDataIds.Add(item.dataId);
                                    }
                                        
                                }
                                else if (item.method == 3)
                                {
                                    item.fromTime1 = "00:00:00";
                                    item.toTime1 = "00:01:00";
                                    item.fromTime2 = "";
                                    item.toTime2 = "";
                                    item.fromTime3 = "";
                                    item.toTime3 = "";
                                    item.fromTime4 = "";
                                    item.toTime4 = "";
                                    if (!PostDoor.SetTime(machine.lLoginID, item))
                                    {
                                        FailureData.Add(new ErrInfo() { DataId = item.dataId, Err = "设置计划模板或者设置计划时间  失败" });
                                    }
                                    else
                                    {
                                        successDataIds.Add(item.dataId);
                                    }
                                       
                                }
                            }

                            //if (successDataIds!= null && successDataIds.Count > 0)
                            //{
                            //    var tempList = string.Join(",", successDataIds);
                            //    url = Common.GetReturnUrl(settings.ReturnDataUrl, machine.MachineSN, tempList, 6);

                            //    urlDB = await HttpHelper.PutUrlAsync(url, null, authorizationToken);
                            //}
                            if ((FailureData != null && FailureData.Count > 0) || (successDataIds.Count>0))
                            {
                                PutDoorUpdateData putDoorUpdate = new()
                                {
                                    FailureData = FailureData,
                                    SuccessDataIds = successDataIds
                                };
                                string json = JsonHepler.ToJson(putDoorUpdate);
                                authorizationToken = Common.SetToken(settings.WebCheckCode1);
                                urlDB = await HttpHelper.PutUrlAsync(settings.DoorUpdateDataUrl, json, authorizationToken);
                                var outData = JsonHepler.FromJson<UpdateData<bool>>(urlDB);
                                IsupLog.Info("{0}", outData.ErrType);
                                IsupLog.Info(outData.Message);
                                IsupLog.Info("{0}", outData.Status);
                            }

                        }
                    }


                }
            }
            catch (Exception ex)
            {
                Common.WriteFileHours("SyncDoorData.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
            }
        }

        /// <summary>
        /// 更新门禁信息
        /// </summary>
        /// <param name="machine">门禁设备信息</param>
        /// <param name="settings">系统配置</param>
        private static async Task SyncDoorData(im_machine machine, Settings settings)
        {
            const int MethodAddUpdate = 1;
            const int MethodAddUpdate2 = 2;
            const int MethodDelete = 3;

            try
            {
                // 1. 前置校验 - 避免空引用异常
                if (machine == null)
                {
                    Common.WriteFileHours("SyncDoorData.txt", true, "门禁设备信息为空");
                    return;
                }

                if (settings == null)
                {
                    Common.WriteFileHours("SyncDoorData.txt", true, "系统配置为空");
                    return;
                }


                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                string apiUrl = Common.GetUrl(settings.DoorUpdateDataUrl, machine.MachineSN, 20);

                string responseData = await HttpHelper.GetUrlAsync(apiUrl, authorizationToken);
                if (string.IsNullOrWhiteSpace(responseData))
                {
                    return;
                }

               
                var updateSetting = JsonHepler.FromJson<UpdateData<List<DoorData>>>(responseData);
                if (updateSetting == null || !updateSetting.Status || updateSetting.Data == null || updateSetting.Data.Count == 0)
                {
                  
                    return;
                }
               
               
                List<int> successDataIds = [];
                List<ErrInfo> failureDataList = [];
                foreach (var doorItem in updateSetting.Data)
                {
                    try
                    {
                        if (doorItem.method == MethodAddUpdate || doorItem.method == MethodAddUpdate2)
                        {
                            await HandleAddOrUpdateDoor(machine, doorItem,DateTime.Now, successDataIds, failureDataList);
                        }
                        else if (doorItem.method == MethodDelete)
                        {
                            HandleDeleteDoorData(machine, doorItem, successDataIds, failureDataList);
                        }
                        else
                        {
                            failureDataList.Add(new ErrInfo
                            {
                                DataId = doorItem.dataId,
                                Err = $"不支持的操作类型：{doorItem.method}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        failureDataList.Add(new ErrInfo
                        {
                            DataId = doorItem.dataId,
                            Err = $"处理门禁数据时发生异常：{ex.Message}"
                        });
                        Common.WriteFileHours("SyncDoorData.txt", true,
                            $"处理门禁数据ID:{doorItem.dataId}异常：{ex}{Environment.NewLine}{ex.StackTrace}");
                    }
                }
                await ReportDoorDataProcessResult(settings, machine.MachineSN, successDataIds, failureDataList);
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException == null
                    ? $"{ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}{ex.Source}"
                    : $"{ex.InnerException.Message}{Environment.NewLine}{ex.InnerException.StackTrace}{Environment.NewLine}{ex.InnerException.Source}";

                Common.WriteFileHours("SyncDoorData.txt", true, errorMsg);
            }
        }

        #region 私有处理门禁数据方法
        /// <summary>
        /// 处理新增/更新类型的门禁数据
        /// </summary>
        private static async Task HandleAddUpdateDoorData(im_machine machine, DoorData doorItem, List<int> successDataIds, List<ErrInfo> failureDataList)
        {
            const string EmptyHolidayGroupNo = "0";
            const string ErrorMsgTemplateTime = "设置计划模板或者设置计划时间失败";
            const int DelayMilliseconds = 5000;

            string holidayGroupNo = string.Empty;
            string strOutPuts = string.Empty;

           
            foreach (var holidayGroupData in doorItem.HolidayGroupDatas ?? Enumerable.Empty<HolidayGroupData>())
            {
                string holidayPeriodNo = string.Empty;

                foreach (var holidayPeriod in holidayGroupData.HolidayPeriodData ?? Enumerable.Empty<HolidayPeriodData>())
                {
                    await Task.Delay(DelayMilliseconds);

                  
                    if (!PostCardHolidayPlan.SetCardHolidayPlan(machine.lLoginID, holidayPeriod, out strOutPuts))
                    {
                        failureDataList.Add(new ErrInfo { DataId = doorItem.dataId, Err = strOutPuts });
                        return;
                    }

                   
                    holidayPeriodNo = string.IsNullOrEmpty(holidayPeriodNo)
                        ? holidayPeriod.HolidayPlanNo
                        : $"{holidayPeriodNo},{holidayPeriod.HolidayPlanNo}";
                }

                if (!string.IsNullOrEmpty(holidayPeriodNo) &&
                    !PostCardHolidayPlan.SetCardHolidayGroup(machine.lLoginID, holidayGroupData.HolidayGroupNo,
                        holidayGroupData.HolidayGroupNo, holidayPeriodNo, out strOutPuts))
                {
                    failureDataList.Add(new ErrInfo { DataId = doorItem.dataId, Err = strOutPuts });
                    return;
                }
                holidayGroupNo = string.IsNullOrEmpty(holidayGroupNo)
                    ? holidayGroupData.HolidayGroupNo
                    : $"{holidayGroupNo},{holidayGroupData.HolidayGroupNo}";
            }
            holidayGroupNo = string.IsNullOrWhiteSpace(holidayGroupNo) ? EmptyHolidayGroupNo : holidayGroupNo;
            bool isTemplateSetSuccess = PostDoor.SetTemplate(machine.lLoginID, doorItem.groupID, doorItem.groupID, holidayGroupNo);
            bool isTimeSetSuccess = PostDoor.SetTime(machine.lLoginID, doorItem);

            if (!isTemplateSetSuccess || !isTimeSetSuccess)
            {
                failureDataList.Add(new ErrInfo { DataId = doorItem.dataId, Err = ErrorMsgTemplateTime });
                return;
            }
            successDataIds.Add(doorItem.dataId);
        }



        /// <summary>
        /// 处理门禁新增/更新逻辑
        /// </summary>
        private static async Task HandleAddOrUpdateDoor(im_machine machine,
            DoorData doorItem,
            DateTime currentTime, List<int> successDataIds, List<ErrInfo> failureDataList)
        {
            var holidayPeriodNoBuilder = new StringBuilder();
            var holidayGroupNoBuilder = new StringBuilder();
            var processedGroupNos = new HashSet<string>(); // 用于记录已处理的假日组编号，防止重复


            OperationDoorResult operationDoorResult = new()
            {
                Success = false,
                IssueCardHoliday = null,
            };
            List<bool> lb = [];
            // 处理假日计划
            if (doorItem.HolidayGroupDatas?.Any() == true)
            {

                List<IssueCardHolidayGroup> IssueCardHolidayList = [];

                //获取这个机器下的假日组


                foreach (var holidayGroup in doorItem.HolidayGroupDatas)
                {

                    List<IssueCardDoorResult> IssueCardHolidayPlanResult = [];

                    var issueCardHolidayGroup = new IssueCardHolidayGroup()
                    {
                        HolidayGroupNo = holidayGroup.HolidayGroupNo,
                        Success = false,
                        RequestXML = string.Empty,
                        ResponseXML = string.Empty,
                        ErrorMessage = string.Empty,
                        IssueCardHolidayPlanResult = []
                    };


                    // 如果该假日组编号已经处理过，则跳过，避免重复配置
                    if (!processedGroupNos.Add(holidayGroup.HolidayGroupNo))
                        continue;

                    holidayPeriodNoBuilder.Clear(); // 重置临时拼接器
                    foreach (var holidayPeriod in holidayGroup.HolidayPeriodData)
                    { 
                        // 配置假日计划
                        var _IssueCardHolidayPlanResult = ISUPDoor.SetCardHolidayPlanResult(machine.lLoginID, holidayPeriod);
                        if (_IssueCardHolidayPlanResult.Success)
                        {

                            if (holidayPeriodNoBuilder.Length > 0) holidayPeriodNoBuilder.Append(",");
                            holidayPeriodNoBuilder.Append(holidayPeriod.HolidayPlanNo);
                        }
                        IssueCardHolidayPlanResult.Add(_IssueCardHolidayPlanResult);
                        lb.Add(_IssueCardHolidayPlanResult.Success);
                    }
                    var holidayPeriodNo = holidayPeriodNoBuilder.ToString(); // 假日计划编号
                    if (!string.IsNullOrWhiteSpace(holidayPeriodNo))
                    {
                        // 配置假日组

                        var _IssueCardHolidayGroupResult = ISUPDoor.SetCardHolidayGroupResult(machine.lLoginID, holidayGroup.HolidayGroupNo,
                            holidayGroup.GroupName, holidayPeriodNo);
                        issueCardHolidayGroup.Success = _IssueCardHolidayGroupResult.Success;
                        issueCardHolidayGroup.RequestXML = _IssueCardHolidayGroupResult.RequestXML;
                        issueCardHolidayGroup.ResponseXML = _IssueCardHolidayGroupResult.ResponseXML;
                        issueCardHolidayGroup.ErrorMessage = _IssueCardHolidayGroupResult.ErrorMessage;

                        if (_IssueCardHolidayGroupResult.Success)
                        {
                            // 拼接假日组编号（仅当第一次遇到该编号时）
                            if (holidayGroupNoBuilder.Length > 0) holidayGroupNoBuilder.Append(",");
                            holidayGroupNoBuilder.Append(holidayGroup.HolidayGroupNo);
                        }
                        lb.Add(_IssueCardHolidayGroupResult.Success);
                    }
                    issueCardHolidayGroup.IssueCardHolidayPlanResult = IssueCardHolidayPlanResult;
                    IssueCardHolidayList.Add(issueCardHolidayGroup);
                }
                operationDoorResult.IssueCardHoliday = IssueCardHolidayList;
            }

            // 处理假日组编号默认值
            var holidayGroupNo = holidayGroupNoBuilder.Length > 0
                ? holidayGroupNoBuilder.ToString()
                : Constants.Default_HolidayGroupNo;

            // 构建门禁数据
            var doorData = new DoorData
            {
                groupID = doorItem.groupID,

                method = doorItem.method,
                TimePeriods = doorItem.TimePeriods,
                fromTime1 = doorItem.fromTime1,
                toTime1 = doorItem.toTime1,
                fromTime2 = doorItem.fromTime2,
                fromTime3 = doorItem.toTime2,
                fromTime4 = doorItem.fromTime3,
                toTime2 = doorItem.toTime2,
                toTime3 = doorItem.toTime3,
                toTime4 = doorItem.toTime4,
                //name = doorItem.name,
            };



            var isSuccess = ISUPDoor.SetUserRightWeekPlanCfgResult(machine.lLoginID, doorData);
            lb.Add(isSuccess.Success);
            operationDoorResult.IssueTimePeriodResult= isSuccess;
            var IssueCardTemplateResult = ISUPDoor.SetUserRightPlanTemplateResult(machine.lLoginID, doorItem.groupID, doorItem.groupID, doorItem.groupID.ToString(), holidayGroupNo);
            operationDoorResult.IssueCardTemplateResult = IssueCardTemplateResult;
            lb.Add(IssueCardTemplateResult.Success);

            

            bool result = lb.Contains(false); // 存在 false 返回 true，否则 false
            operationDoorResult.Success = !result;
            string operationDoorResult_str = string.Empty;
            

            if (result) //下发失败
            {
                
                string IssueCardHolidayErr=string.Empty;
                if (operationDoorResult.IssueCardHoliday!=null && operationDoorResult.IssueCardHoliday != null)
                {
                    foreach (var item in operationDoorResult.IssueCardHoliday)
                    {
                        IssueCardHolidayErr += "   HolidayGroupNo："+ item.HolidayGroupNo+ "；ErrorMessage：" + item.ErrorMessage;
                    }
                }
                string IssueTimePeriodErr = string.Empty;
                if (operationDoorResult.IssueTimePeriodResult!= null)
                {
                    IssueTimePeriodErr= operationDoorResult.IssueTimePeriodResult.ErrorMessage;
                }
                string IssueCardTemplateErr = string.Empty;

                if (operationDoorResult.IssueCardTemplateResult != null)
                {
                    IssueCardTemplateErr= operationDoorResult.IssueCardTemplateResult.ErrorMessage;
                }
                var err = new
                {
                    IssueCardHolidayErr=IssueCardHolidayErr,
                    IssueTimePeriodErr,
                    IssueCardTemplateErr,
                    isSuccess
                };
                operationDoorResult_str = JsonHepler.ToJson(err);
                failureDataList.Add(new ErrInfo()
                {
                    DataId = doorItem.dataId,
                    Err = operationDoorResult_str
                });


                string errorMsg = JsonHepler.ToJson(operationDoorResult);
                Common.WriteFileHours("HandleAddOrUpdateDoor.txt",true, $"machine：{machine.MachineSN}；errorMsg：{errorMsg}");
            }
            else
            {
                successDataIds.Add(doorItem.dataId);

                string errorMsg = JsonHepler.ToJson(operationDoorResult);
                Common.WriteFileHours("HandleAddOrUpdateDoor.txt", true, $"machine：{machine.MachineSN}；Msg：{errorMsg}");
            }
            await Task.Delay(300);


        }



        /// <summary>
        /// 处理删除类型的门禁数据
        /// </summary>
        private static void HandleDeleteDoorData(im_machine machine, DoorData doorItem, List<int> successDataIds, List<ErrInfo> failureDataList)
        {
            const string DefaultTimeFrom = "00:00:00";
            const string DefaultTimeTo = "00:01:00";
            const string ErrorMsgTemplateTime = "设置计划模板或者设置计划时间失败";

            // 重置时间参数
            doorItem.fromTime1 = DefaultTimeFrom;
            doorItem.toTime1 = DefaultTimeTo;
            doorItem.fromTime2 = string.Empty;
            doorItem.toTime2 = string.Empty;
            doorItem.fromTime3 = string.Empty;
            doorItem.toTime3 = string.Empty;
            doorItem.fromTime4 = string.Empty;
            doorItem.toTime4 = string.Empty;

            // 设置门禁时间
            if (!PostDoor.SetTime(machine.lLoginID, doorItem))
            {
                failureDataList.Add(new ErrInfo { DataId = doorItem.dataId, Err = ErrorMsgTemplateTime });
                return;
            }

            // 处理成功，记录ID
            successDataIds.Add(doorItem.dataId);
        }

        /// <summary>
        /// 上报门禁数据处理结果
        /// </summary>
        private static async Task ReportDoorDataProcessResult(Settings settings, string machineSN, List<int> successDataIds, List<ErrInfo> failureDataList)
        {
            if (successDataIds.Count == 0 && failureDataList.Count == 0)
            {
                return;
            }
            PutDoorUpdateData reportData = new PutDoorUpdateData
            {
                FailureData = failureDataList,
                SuccessDataIds = successDataIds
            };

            try
            {
                
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                string response = await HttpHelper.PutUrlAsync(settings.DoorUpdateDataUrl, reportData, authorizationToken);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    var reportResult = JsonHepler.FromJson<UpdateData<bool>>(response);
                    IsupLog.Info($"上报结果 - 错误类型：{reportResult.ErrType}");
                    IsupLog.Info($"上报结果 - 消息：{reportResult.Message}");
                    IsupLog.Info($"上报结果 - 状态：{reportResult.Status}");
                }
                
            }
            catch (Exception ex)
            {
                Common.WriteFileHours("SyncDoorData.txt", true,
                    $"上报门禁处理结果异常：{ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        }
        #endregion

        ///// <summary>
        ///// 分类别更新人员信息操作
        ///// </summary>
        //private static async Task SortOperationAsync(string Prefix, string Url, int ReturnDataType, im_machine machine)
        //{
        //    var successIdList = new List<string>();//成功ID列表
        //    var failIdList = new List<returnErrorModel>();//失败ID列表
        //    var tempIdList = new List<returnErrorModel>();//临时ID列表，当有成功的更新才进行结算
        //    var url = Common.GetUrl(Url, machine, settings.RecordNumber); //每次下发条数
        //    var urlDB = await Common.PostUrlAsync(url); // 异步调用 PostUrl
        //    if (!string.IsNullOrWhiteSpace(urlDB))
        //    {
        //        var UpdateData = JsonHepler.FromJson<UpdateData<List<Detail>>>(urlDB);
        //        if (UpdateData != null && UpdateData.Status)
        //        {
        //            var groupInfo = UpdateData.Data.GroupBy(w => w.webId).ToList(); //将更新归类
        //            var ret = false;
        //            string tempList = string.Empty;

        //            var employeeNos = UpdateData.Data.Select(w => Prefix + w.webId).ToList();
        //            ISUPPostPerson.GetUserInfoSearch(machine, employeeNos, settings.RecordNumber, out List<UserInfo> userInfoList); // 异步调用 GetUserInfoSearch

        //            foreach (var group in groupInfo)
        //            {
        //                foreach (var item in group)
        //                {
        //                    // 获取这一批已经存在的用户
        //                    var userInfo = userInfoList.FirstOrDefault(w => w.EmployeeNo == Prefix + item.webId);
        //                    //switch (item.method)
        //                    //{
        //                    //    case 1: ret = AddPerson(machine, Prefix, item, userInfo, out tempIdList); break;
        //                    //    case 2: ret = UpdatePerson(machine, Prefix, item, userInfo, ref tempIdList); break;
        //                    //    case 3: ret = DeletePerson(machine, Prefix, item, ref tempIdList); break;
        //                    //}
        //                    if (ret) //成功后，之前的更新均视为成功
        //                    {
        //                        foreach (var id in tempIdList)
        //                            successIdList.Add(id.DataId.ToString());
        //                        tempIdList.Clear(); // 成功结算后，清空临时ID列表
        //                    }
        //                }
        //                ret = false;
        //                if (tempIdList.Count > 0) // 如果还有临时ID列表（说明里面均为失败）
        //                {
        //                    tempIdList = tempIdList.FindAll(w => (w.Err != "跳过"));
        //                    if (tempIdList.Count > 0) // 如果错误类型为跳过，则不进行更新
        //                        failIdList.AddRange(tempIdList);
        //                    tempIdList.Clear(); // 失败结算后，清空临时ID列表
        //                }
        //                if (successIdList.Count + failIdList.Count >= 25)
        //                {
        //                    if (successIdList.Count > 0)
        //                    {
        //                        tempList = string.Join(",", successIdList);
        //                        url = Common.GetReturnUrl(settings.ReturnDataUrl, machine, string.Join(",", successIdList), ReturnDataType);
        //                        NameValueCollection vars = new()
        //                        {
        //                    { "DataId", tempList }
        //                };
        //                        await Common.PostUrlAsync(url); // 异步调用 WebPost
        //                        successIdList.Clear();
        //                    }
        //                    if (failIdList.Count > 0)
        //                    {
        //                        tempList = string.Empty;
        //                        url = Common.GetReturnUrl(settings.ReturnErrorUrl, machine, null, ReturnDataType);
        //                        await Common.PostUrlAsync(url, failIdList); // 异步调用 PostUrl
        //                        failIdList.Clear();
        //                    }
        //                }
        //            }
        //            if (successIdList.Count > 0)
        //            {
        //                tempList = string.Join(",", successIdList);
        //                url = Common.GetReturnUrl(settings.ReturnDataUrl, machine, tempList, ReturnDataType);
        //                //NameValueCollection vars = new NameValueCollection();
        //                //vars.Add("DataId", tempList);
        //                await Common.PostUrlAsync(url); // 异步调用 WebPost
        //            }
        //            if (failIdList.Count > 0)
        //            {
        //                tempList = string.Empty;
        //                url = Common.GetReturnUrl(settings.ReturnErrorUrl, machine, null, ReturnDataType);
        //                await Common.PostUrlAsync(url, failIdList); // 异步调用 PostUrl
        //            }

        //            await Task.Delay(5000);
        //        }
        //    }
        //}


        /// <summary>
        /// 设置请假同步数据
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static async Task SetLeaveSyncAsync(im_machine machine, Settings settings)
        {
            try
            {
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.LeaveSyncDatas, machine.MachineSN, settings.RecordNumber); //每次下发条数
                var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<List<HaiKangLeaveSyncData>>>(urlDB);
                    if (UpdateSetting == null)
                    {
                        return;
                    }
                    var succeedIds = new List<Guid>();

                    foreach (var item in UpdateSetting.Data)
                    {
                        Person person = new()
                        {
                            PersonId = item.PersonId,
                            PlanTemplateNo = item.PlanTemplateNo,
                            BeginTime = item.BeginTime,
                            EndTime = item.EndTime,
                            PersonType = item.PersonType,
                            ValidEnable = true
                        };
                        if (ISUPPostPerson.SetPost(machine, person))
                        {
                            succeedIds.Add(item.Id);
                        }
                    }
                    try
                    {
                        if (UpdateSetting.Data!=null&& UpdateSetting.Data.Count>0&& settings.IsMessageQueue)
                        {
                            string str = JsonHepler.ToJson(UpdateSetting.Data);
                            using MessageQueue mq = MessageQueueHepler.Create(settings.MessageQueueAddress_MachineLeave);
                            mq.Send(str);
                        }
                        
                    }
                    catch (Exception)
                    {
                        // 处理消息队列发送失败的情况
                    }
                    if (succeedIds.Count>0)
                    {
                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        url = Common.GetUrl(settings.UpdateLeave, machine.MachineSN); //每次下发条数
                         urlDB = await HttpHelper.PutUrlAsync(url, succeedIds, authorizationToken); // 异步调用 PostUrl

                        
                    }


                }
            }
            catch (Exception ex)
            {

                Common.WriteFileHours("SetLeaveSyncAsync.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
            }
        }


        /// <summary>
        /// 设置请假恢复数据
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static async Task SetLeaveResetSyncAsync(im_machine machine, Settings settings)
        {
            try
            {
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.LeaveResetSyncDatas, machine.MachineSN, settings.RecordNumber); //每次下发条数
                var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<List<HaiKangLeaveSyncData>>>(urlDB);
                    if (UpdateSetting == null)
                    {
                        return;
                    }
                    var succeedIds = new List<Guid>();

                    foreach (var item in UpdateSetting.Data)
                    {
                        Person person = new()
                        {
                            PersonId = item.PersonId,
                            
                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            PersonType = item.PersonType,
                            ValidEnable = false,
                             PlanTemplateNo = item.PlanTemplateNo,
                             
                        };
                        if (ISUPPostPerson.SetPost(machine, person))
                        {
                            succeedIds.Add(item.Id);
                        }
                    }

                    if (succeedIds.Count > 0)
                    {
                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        url = Common.GetUrl(settings.UpdateReset, machine.MachineSN); //每次下发条数
                        urlDB = await HttpHelper.PutUrlAsync(url, succeedIds, authorizationToken); // 异步调用 PostUrl
                    }


                }
            }
            catch (Exception ex)
            {

                Common.WriteFileHours("SetLeaveResetSyncAsync.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
            }
        }
        private static async Task SetAcsEvent(im_machine machine, Settings settings)
        {
            try
            {
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.SyncAcsEventUrl, machine.MachineSN);
                var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken);

                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<GetAcsEventModel>>(urlDB);

                    if (UpdateSetting == null)
                    {
                        return;
                    }

                    if (UpdateSetting.Status && UpdateSetting.Data != null)
                    {
                        var syncAcsEventResult = UpdateSetting.Data;

                        //2026-01-01T00:00:00+08:00
                        var da = ISUPAcsEvent.GetAcsEvent(machine, syncAcsEventResult);
                        string json_data = string.Empty;
                        int int_state = 1;
                        if (da != null && da.Count > 0)
                        {
                            json_data = JsonHepler.ToJson(da);
                            int_state = 3;
                        }
                        else
                        {
                            int_state = 2;
                        }

                        SetAcsEvent s = new()
                        {

                            Data = json_data,
                            State = int_state,
                            Id = syncAcsEventResult.Id,
                        };

                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        string q = await HttpHelper.PutUrlAsync(settings.SyncAcsEventUrl, s, authorizationToken);
                        IsupLog.Info(q);

                    }

                }


            }
            catch (Exception)
            {


            }
        }

    }
}
