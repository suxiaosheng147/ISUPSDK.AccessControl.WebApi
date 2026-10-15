using ISUPSDK.AccessControl.WebApi.Services.Logging;
using Experimental.System.Messaging;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Distribute;
using ISUPSDK.AccessControl.WebApi.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;

namespace ISUPSDK.AccessControl.WebApi.Customize
{
    public class SyncAuthRuleInformation
    {
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
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.PutInfoData, machine.MachineSN, 1);
                string urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken);
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<SyncData>>(urlDB);
                    if (UpdateSetting == null)
                    {
                        return;
                    }
                    if (UpdateSetting.Status && UpdateSetting.Data != null)
                    {
                        bool NeedInit = UpdateSetting.Data.NeedInit;

                        if (NeedInit)
                            if (!await AllInit(machine, settings)) // 异步调用 AllInit
                                return;
                        if (UpdateSetting.Data.DoorCount > 0 && !NeedInit)
                        {
                            await SyncDoorData(machine, settings);
                        }
                        if (UpdateSetting.Data.AuthRulePersonData > 0 && !NeedInit)
                        {
                            await SetPersonAsync(machine, settings);
                        }

                        await SetAcsEvent(machine, settings);

                        if (UpdateSetting.Data.LeaveResetSyncCount > 0 && !NeedInit)
                        {
                            await SetLeaveResetSyncAsync(machine, settings);
                        }

                        if (UpdateSetting.Data.LeaveSyncCount > 0 && !NeedInit)
                        {
                            await SetLeaveSyncAsync(machine, settings);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Common.WriteFileHours("SyncDevicesAsync.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
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
            //for (var i = 1; i < 30; i++)
            //{
            //    PostDoor.SetTemplate(machine.lLoginID, i, i);
            //    PostDoor.Start(machine.lLoginID, i);
            //    var doorData = new DoorData { groupID = i, fromTime1 = "00:00:00", toTime1 = "00:00:10" };
            //    PostDoor.SetTime(machine.lLoginID, doorData);
            //}


            //#region 全天候不通行模板
            //PostDoor.SetTemplate(machine.lLoginID, 99, 99);
            //PostDoor.Start(machine.lLoginID, 99);
            //var doorData1 = new DoorData { groupID = 99, fromTime1 = "00:00:00", toTime1 = "00:01:00" };
            //PostDoor.SetTime(machine.lLoginID, doorData1);
            //#endregion

            //#region 全天候通行模板 
            //PostDoor.SetTemplate(machine.lLoginID, 98, 98);
            //PostDoor.Start(machine.lLoginID, 98);
            //var doorData2 = new DoorData { groupID = 98, fromTime1 = "00:00:00", toTime1 = "23:59:59" };
            //PostDoor.SetTime(machine.lLoginID, doorData2);
            //#endregion


            for (var i = 1; i < 30; i++)
            {
                
                var doorData = new DoorData { groupID = i, fromTime1 = "00:00:00", toTime1 = "00:00:10" , TimePeriods=new List<TimePeriod>() { 
                     new TimePeriod()
                     {
                          FromTime1="00:00:00",
                           Week=0,
                            ToTime1="00:00:10"

                     },
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=1,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=2,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=3,

                     }
                ,
                new TimePeriod()
                     {
                         FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=4,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=5,

                     },
                 new TimePeriod()
                     {
                         FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=6,

                     }
                } };
                var isSuccess = ISUPDoor.SetUserRightWeekPlanCfgResult(machine.lLoginID, doorData);
                var IssueCardTemplateResult = ISUPDoor.SetUserRightPlanTemplateResult(machine.lLoginID, doorData.groupID, doorData.groupID, doorData.groupID.ToString(), "0");

                Common.WriteFileHours("AllInit.txt", true, "isSuccess "+ +i+":" + isSuccess.ErrorMessage);
                Common.WriteFileHours("AllInit.txt", true, "IssueCardTemplateResult "+ +i+":" + IssueCardTemplateResult.ErrorMessage);


            }

            var doorData2 = new DoorData { groupID = 98, fromTime1 = "00:01:00", toTime1 = "23:55:00" ,TimePeriods = new List<TimePeriod>() {
                     new TimePeriod()
                     {
                          FromTime1="00:01:00", ToTime1 = "23:55:00",
                           Week=0,

                     },
                new TimePeriod()
                     {
                          FromTime1="00:01:00", ToTime1 = "23:55:00",
                           Week=1,

                     }
                ,
                new TimePeriod()
                     {
                           FromTime1="00:01:00", ToTime1 = "23:55:00",
                           Week=2,

                     }
                ,
                new TimePeriod()
                     {
                           FromTime1="00:01:00",ToTime1 = "23:55:00",
                           Week=3,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:01:00", ToTime1 = "23:55:00",
                           Week=4,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:01:00", ToTime1 = "23:55:00",
                           Week=5,

                     },
                 new TimePeriod()
                     {
                          FromTime1="00:01:00",ToTime1 = "23:55:00",
                           Week=6,

                     }



                }
            };

            var isSuccess2 = ISUPDoor.SetUserRightWeekPlanCfgResult(machine.lLoginID, doorData2);
            var IssueCardTemplateResult2 = ISUPDoor.SetUserRightPlanTemplateResult(machine.lLoginID, doorData2.groupID, doorData2.groupID, doorData2.groupID.ToString(), "0");

            Common.WriteFileHours("AllInit.txt", true, "isSuccess98 " + isSuccess2.ErrorMessage);
            Common.WriteFileHours("AllInit.txt", true, "IssueCardTemplateResult98 " + isSuccess2.ErrorMessage);



            var doorData1 = new DoorData { groupID = 99, fromTime1 = "00:00:00", toTime1 = "00:01:00",
                TimePeriods = new List<TimePeriod>() {
                      new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=0,

                     },
                new TimePeriod()
                     {
                          FromTime1="00:00:00", ToTime1="00:00:10",
                           Week=1,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00", ToTime1="00:00:10",
                           Week=2,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=3,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=4,

                     }
                ,
                new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=5,

                     },
                 new TimePeriod()
                     {
                          FromTime1="00:00:00",  ToTime1="00:00:10",
                           Week=6,

                     }



                }
            };

            var isSuccess1 = ISUPDoor.SetUserRightWeekPlanCfgResult(machine.lLoginID, doorData1);
            var IssueCardTemplateResult1 = ISUPDoor.SetUserRightPlanTemplateResult(machine.lLoginID, doorData1.groupID, doorData1.groupID, doorData1.groupID.ToString(), "0");

            Common.WriteFileHours("AllInit.txt", true, "isSuccess99 "+ isSuccess1.ErrorMessage);
            Common.WriteFileHours("AllInit.txt", true, "IssueCardTemplateResult99 " + IssueCardTemplateResult1.ErrorMessage);

           

            string authorizationToken = Common.SetToken(settings.WebCheckCode1);
            var url = Common.GetUrl(settings.ReturnInitUrl, machine.MachineSN);
            string urlDB = await HttpHelper.PutUrlAsync(url, null, authorizationToken); // 异步调用 PostUrl
            if (!string.IsNullOrWhiteSpace(urlDB))
            {
                var UpdateSetting = JsonHepler.FromJson<UpdateData<object>>(urlDB);
                if (UpdateSetting == null)
                {
                    return false;
                }
                else
                {
                    return true;
                }

                
            }
            return false;
        }

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
                var url = Common.GetUrl(settings.AuthRuleLeaveSyncDatasUrl, machine.MachineSN, settings.RecordNumber); //每次下发条数
                var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<List<LeaveSyncData>>>(urlDB);
                    if (UpdateSetting == null)
                    {
                        return;
                    }
                    var succeedIds = new List<Guid>();
                    List<PutSyncLeaveData> setData = [];
                    foreach (var item in UpdateSetting.Data)
                    {
                        Person person = new()
                        {
                            PersonId = item.PersonId,
                            PlanTemplateNo = item.DoorCode.ToString(),
                            BeginTime = item.BeginTime,
                            EndTime = item.EndTime,
                            PersonType = item.PersonType,
                            ValidEnable = true
                        };
                        if (ISUPPostPerson.SetPost(machine, person))
                        {
                            //succeedIds.Add(item.Id);
                            setData.Add(new PutSyncLeaveData()
                            {
                                ErrorMessage = "",
                                ErrorNumber = 0,
                                ErrorTime = null,
                                Id = item.Id,
                                State = 3

                            });
                        }
                    }
                    try
                    {
                        if (UpdateSetting.Data != null && UpdateSetting.Data.Count > 0 && settings.IsMessageQueue)
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
                    if (setData.Count > 0)
                    {
                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        url = Common.GetUrl(settings.AuthRuleUpdateLeaveUrl, machine.MachineSN); //每次下发条数
                        urlDB = await HttpHelper.PutUrlAsync(url, setData, authorizationToken); // 异步调用 PostUrl


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
                var url = Common.GetUrl(settings.AuthRuleLeaveResetSyncDatasUrl, machine.MachineSN, settings.RecordNumber); //每次下发条数
                var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl
                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateSetting = JsonHepler.FromJson<UpdateData<List<LeaveResetSyncData>>>(urlDB);
                    if (UpdateSetting == null)
                    {
                        return;
                    }
                    var succeedIds = new List<Guid>();
                    List<PutSyncLeaveData> setData = [];
                    foreach (var item in UpdateSetting.Data)
                    {
                        Person person = new()
                        {
                            PersonId = item.PersonId,

                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            PersonType = item.PersonType,
                            ValidEnable = false,
                            PlanTemplateNo = item.DoorCode.ToString(),

                        };
                        if (ISUPPostPerson.SetPost(machine, person))
                        {
                            //succeedIds.Add(item.Id);
                            setData.Add(new PutSyncLeaveData()
                            {
                                ErrorMessage = "",
                                ErrorNumber = 0,
                                ErrorTime = null,
                                Id = item.Id,
                                State = 3

                            });
                        }
                    }

                    if (setData.Count > 0)
                    {
                        authorizationToken = Common.SetToken(settings.WebCheckCode1);
                        url = Common.GetUrl(settings.AuthRuleUpdateLeaveUrl, machine.MachineSN); //每次下发条数
                        urlDB = await HttpHelper.PutUrlAsync(url, setData, authorizationToken); // 异步调用 PostUrl
                    }


                }
            }
            catch (Exception ex)
            {

                Common.WriteFileHours("SetLeaveResetSyncAsync.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
            }
        }

        private static async Task SetPersonAsync(im_machine machine, Settings settings)
        {
            try
            {
                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.SyncPersonData, machine.MachineSN, settings.RecordNumber); //每次下发条数
                var urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken); // 异步调用 PostUrl

                if (!string.IsNullOrWhiteSpace(urlDB))
                {
                    var UpdateData = JsonHepler.FromJson<UpdateData<List<SyncPersonDataPage>>>(urlDB);
                    if (UpdateData != null && UpdateData.Status)
                    {
                        if (UpdateData.Data != null && UpdateData.Data.Count > 0)
                        {
                            List<PutSyncPersonData> putSyncPersonDatas = [];
                            List<UserInfo> UserInfo = [];

                            foreach (var item in UpdateData.Data)
                            {
                                int State = 1;
                                OperationMachineResult operationMachineResult = new();
                                string ErrorMessage = string.Empty;
                                switch (item.Method)
                                {
                                    case 1:
                                        operationMachineResult = await AddPersonAsync(machine, settings, item);
                                        State=operationMachineResult.Success ? 3 : 2;
                                        break;
                                    case 2:
                                        operationMachineResult = await AddPersonAsync(machine, settings, item);
                                        State = operationMachineResult.Success ? 3 : 2;
                                        break;
                                    case 3:
                                        operationMachineResult = DeletePersonInfo(machine, item);
                                        State = operationMachineResult.Success ? 3 : 2;

                                        break;

                                    default:
                                        break;
                                }
                                putSyncPersonDatas.Add(new PutSyncPersonData()
                                {
                                    Id = item.Id,
                                    ErrorMessage = ErrorMessage,
                                    State = State,
                                    IssuePersonCardResult = operationMachineResult.IssuePersonCardResult,
                                    IssuePersonInfoResult = operationMachineResult.IssuePersonInfoResult,
                                    IssuePersonFaceResult = operationMachineResult.IssuePersonFaceResult,
                                    SourceId_List = item.SourceId_List,
                                    SourceId_List1 = item.SourceId_List1,

                                });
                            }
                            if (putSyncPersonDatas != null && putSyncPersonDatas.Count > 0)
                            {

                                authorizationToken = Common.SetToken(settings.WebCheckCode1 ?? "");
                               
                                await HttpHelper.PutUrlAsync(settings.SyncPersonData, putSyncPersonDatas, authorizationToken);

                               
                            }
                            

                        }
                    }
                }
            }
            catch (Exception)
            {

                
            }

        }


        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="person"></param>
        /// <returns></returns>
        public static (bool,string) DeletePerson(im_machine machine, SyncPersonDataPage person)
        {
            try
            {
                var ret = ISUPPostPerson.DelPerson(machine, person,30000,500);

               
                return ret;
            }
            catch (Exception)
            {

                return (false,"删除失败");
            }
        }
        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="person"></param>
        /// <returns></returns>
        public static  OperationMachineResult DeletePersonInfo(im_machine machine, SyncPersonDataPage person)
        {
            OperationMachineResult result = new();
            try
            {
                var ret = ISUPPostPerson.DelPersonInfo(machine, person, 30000, 500);


                result.IssuePersonInfoResult= ret;
                result.Success = ret.Success;

                return result;
            }
            catch (Exception)
            {

                return result;
            }
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="person"></param>
        /// <returns></returns>
        public static async Task<OperationMachineResult> AddPersonAsync(im_machine machine, Settings settings, SyncPersonDataPage person)
        {

            OperationMachineResult result = new();
            try
            {
                bool isSuccess = false;
                var ret1 = false;
                var per_person = ISUPPostPerson.PostPerson(machine, person);
                ret1 = per_person.Success;
                result.IssuePersonInfoResult=per_person;
                isSuccess = ret1;

                // 第二步：如果有卡ID并且机器支持读卡，添加卡信息
                bool ret2 = false;
                if (!string.IsNullOrWhiteSpace(person.CardId) && ret1)
                {
                    var per_card = ISUPPostCard.PostCard(machine, false, person);
                    ret2 = per_card.Success;
                    result.IssuePersonCardResult=per_card;
                    isSuccess = ret2;
                }
                // 第三步：如果需要更新照片，并且成功添加基本信息，则添加人脸信息
                bool ret3 = false;
                if (ret1 && person.Pic != "" && person.IsUpdatePic)
                {
                    var per = await ISUPPostFace.PUTFace(machine, settings, person);
                    ret3 = per.Success;
                    result.IssuePersonFaceResult=per;
                    isSuccess = ret3;
                }
                result.Success = isSuccess;

                return result;
            }
            catch (Exception)
            {

                return result;
            }
        }

        /// <summary>
        /// 更新门禁信息
        /// </summary>
        /// <param name="machine">门禁设备实体</param>
        /// <param name="settings">系统配置</param>
        private static async Task SyncDoorData(im_machine machine, Settings settings)
        {
            // 2. 入参校验：提前拦截无效参数，避免后续空指针
            if (machine == null)
            {
                Common.WriteFileHours("SyncDoorData.txt", true, "入参异常：machine不能为空");
                return;
            }
            if (settings == null)
            {
                Common.WriteFileHours("SyncDoorData.txt", true, "入参异常：settings不能为空");
                return;
            }
            if (machine.lLoginID <= -1)
            {
                Common.WriteFileHours("SyncDoorData.txt", true, $"设备登录ID无效：MachineSN={machine.MachineSN}, lLoginID={machine.lLoginID}");
                return;
            }

            try
            {
               
                var authorizationToken = Common.SetToken(settings.WebCheckCode1 ?? string.Empty);
                var requestUrl = Common.GetUrl(settings.SyncDoorData, machine.MachineSN, 1);
                var currentTime = DateTime.Now;
               
                string doorDataResponse = await HttpHelper.GetUrlAsync(requestUrl, authorizationToken);

                if (string.IsNullOrWhiteSpace(doorDataResponse))
                {
                    Common.WriteFileHours("SyncDoorData.txt", true, $"设备{machine.MachineSN}未获取到门禁同步数据");
                    return;
                }
                var updateSetting = JsonHepler.FromJson<UpdateData<List<AuthRuleDoorData>>>(doorDataResponse);
                if (updateSetting == null || !updateSetting.Status || updateSetting.Data == null || updateSetting.Data.Count == 0)
                {
                    Common.WriteFileHours("SyncDoorData.txt", true, $"设备{machine.MachineSN}同步数据无效：Status={updateSetting?.Status}, 数据量={updateSetting?.Data?.Count ?? 0}");
                    return;
                }
                var syncResultList = new List<PutSyncDoorData>();

                foreach (var doorItem in updateSetting.Data)
                {
                    try
                    {
                        switch (doorItem.Method)
                        {
                            // 新增/更新门禁（原Method=1/2）
                            case Constants.Method_AddOrUpdate:
                            case Constants.Method_AddOrUpdate_Alternative:
                                HandleAddOrUpdateDoor(machine, doorItem, currentTime, syncResultList);
                                break;

                            // 删除门禁（原Method=3）
                            case Constants.Method_Delete:
                                HandleDeleteDoor(machine, doorItem, currentTime, syncResultList);
                                break;

                            // 未知操作类型
                            default:
                                syncResultList.Add(CreateSyncResult(doorItem.Id, Constants.ErrorNumber_Failure,
                                    $"不支持的操作类型：Method={doorItem.Method}", currentTime, Constants.State_Failure));
                                Common.WriteFileHours("SyncDoorData.txt", true, $"设备{machine.MachineSN}门禁项{doorItem.Id}操作类型无效：{doorItem.Method}");
                                break;
                        }
                    }
                    catch (Exception itemEx)
                    {

                        
                        var errorMsg = $"门禁项{doorItem.Id}处理异常：{itemEx.Message}\n{itemEx.StackTrace}";
                        Common.WriteFileHours("SyncDoorData.txt", true, errorMsg);
                        syncResultList.Add(CreateSyncResult(doorItem.Id, Constants.ErrorNumber_Failure, errorMsg, currentTime, Constants.State_Failure));
                    }
                }

                if (syncResultList.Any())
                {
                    
                    authorizationToken = Common.SetToken(settings.WebCheckCode1 ?? string.Empty);
                    requestUrl = Common.GetUrl(settings.SyncDoorData, machine.MachineSN);
                    await HttpHelper.PutUrlAsync(requestUrl, syncResultList, authorizationToken);
                }
            }
            catch (Exception ex)
            {
               
                var errorDetail = ex.InnerException ?? ex;
                var errorMsg = $"设备{machine?.MachineSN ?? "未知"}同步门禁数据全局异常：{errorDetail.Message}\n堆栈：{errorDetail.StackTrace}\n来源：{errorDetail.Source}";
                Common.WriteFileHours("SyncDoorData.txt", true, errorMsg);
            }

        }

        #region 私有辅助方法
        /// <summary>
        /// 创建同步结果对象
        /// </summary>
        private static PutSyncDoorData CreateSyncResult(Guid id, int errorNumber, string errorMsg, DateTime? errorTime, int state)
        {
            return new PutSyncDoorData
            {
                Id = id,
                ErrorMessage = errorMsg,
                ErrorNumber = errorNumber,
                ErrorTime = errorTime,
                State = state
            };
        }

        

        /// <summary>
        /// 处理门禁新增/更新逻辑
        /// </summary>
        private static void HandleAddOrUpdateDoor(im_machine machine,
            AuthRuleDoorData doorItem,
            DateTime currentTime, 
            List<PutSyncDoorData> syncResultList)
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
                groupID = doorItem.Code,
                
                method = doorItem.Method,
                TimePeriods = doorItem.TimePeriods
            };

            // 配置门禁模板和时段
            var isSuccess = ISUPDoor.SetUserRightWeekPlanCfgResult(machine.lLoginID, doorData);
            var IssueCardTemplateResult = ISUPDoor.SetUserRightPlanTemplateResult(machine.lLoginID, doorItem.Code, doorItem.Code, doorItem.Code.ToString(), holidayGroupNo);
            operationDoorResult.IssueCardTemplateResult = IssueCardTemplateResult;
            lb.Add(IssueCardTemplateResult.Success);


            lb.Add(isSuccess.Success);

            bool result = lb.Contains(false); // 存在 false 返回 true，否则 false
            operationDoorResult.Success = !result;
            string operationDoorResult_str=string.Empty;
            operationDoorResult_str = JsonHepler.ToJson(operationDoorResult);
            syncResultList.Add(new PutSyncDoorData()
            {
                Id = doorItem.Id,
                State = result ? 2 : 3,
                ErrorMessage = string.Empty,
                ErrorNumber = result ? 1 : 0,
                ErrorTime = null,
                OperationDoorResult = HttpUtility.UrlEncode(operationDoorResult_str),
                
            });
        }



        /// <summary>
        /// 处理门禁删除逻辑
        /// </summary>
        private static void HandleDeleteDoor(im_machine machine, AuthRuleDoorData doorItem, DateTime currentTime, List<PutSyncDoorData> syncResultList)
        {
            var doorData = new DoorData
            {
                fromTime1 = Constants.Default_TimeRange,
                toTime1 = Constants.Default_DeleteTimeRange_End,
                fromTime2 = string.Empty,
                toTime2 = string.Empty,
                fromTime3 = string.Empty,
                toTime3 = string.Empty,
                fromTime4 = string.Empty,
                toTime4 = string.Empty
            };

            var isSuccess = PostDoor.SetTime(machine.lLoginID, doorData);
            if (isSuccess)
            {
                syncResultList.Add(CreateSyncResult(doorItem.Id, Constants.ErrorNumber_Success,
                    string.Empty, null, Constants.State_Success));
            }
            else
            {
                syncResultList.Add(CreateSyncResult(doorItem.Id, Constants.ErrorNumber_Failure,
                    Constants.ErrorMsg_DoorDelete, currentTime, Constants.State_Failure));
            }
        }
        #endregion



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
                   
                    if (UpdateSetting.Status && UpdateSetting.Data != null )
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
                        string q= await HttpHelper.PutUrlAsync(settings.SyncAcsEventUrl, s, authorizationToken);
                        IsupLog.Info(q);

                    }
                    
                }


            }
            catch (Exception)
            {

                
            }
        }
    }
    // 1. 常量定义：抽离魔法值，提升可维护性
    public static class Constants
    {
        // 操作类型
        public const int Method_AddOrUpdate = 1;
        public const int Method_AddOrUpdate_Alternative = 2;
        public const int Method_Delete = 3;

        // 错误码/状态码
        public const int ErrorNumber_Success = 0;
        public const int ErrorNumber_Failure = 5;
        public const int State_Failure = 2;
        public const int State_Success = 3;

        // 错误提示
        public const string ErrorMsg_HolidayPlan = "设置失败：访问权限假日计划配置失败";
        public const string ErrorMsg_HolidayGroup = "设置失败：访问权限假日组配置失败";
        public const string ErrorMsg_DoorTemplate = "设置失败：门禁模板/时段配置失败";
        public const string ErrorMsg_DoorDelete = "设置失败：门禁删除操作失败";

        // 默认值
        public const string Default_HolidayGroupNo = "0";
        public const string Default_TimeRange = "00:00:00";
        public const string Default_DeleteTimeRange_End = "00:01:00";
    }

}
