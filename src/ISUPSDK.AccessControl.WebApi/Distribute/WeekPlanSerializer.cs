using Newtonsoft.Json;
namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public static class WeekPlanSerializer
    {
        // 星期映射（1->Monday, 2->Tuesday, ...）
        private static readonly Dictionary<int, string> WeekNameMap = new(){
            {0, "Monday"}, 
            {1, "Tuesday"}, 
            {2, "Wednesday"},
            {3, "Thursday"}, 
            {4, "Friday"}, 
            {5, "Saturday"}, 
            {6, "Sunday"}
        };
        //List<TimePeriod> period =
        //                   [
        //                       ToTimePeriod(0, weekTime.MondayTimeGroup),
        //                        ToTimePeriod(1, weekTime.TuesdayTimeGroup),
        //                        ToTimePeriod(2, weekTime.WednesdayTimeGroup),
        //                        ToTimePeriod(3, weekTime.ThursdayTimeGroup),
        //                        ToTimePeriod(4, weekTime.FridayTimeGroup),
        //                        ToTimePeriod(5, weekTime.SaturdayTimeGroup),
        //                        ToTimePeriod(6, weekTime.SundayTimeGroup),
        //                    ];
        // 默认时间字符串（格式 "HH:mm:ss"）
        private const string DefaultTime = "00:00:00";

        /// <summary>
        /// 将 List<TimePeriod> 转换为 JSON 字符串，无效时间段输出默认时间且 enable=false
        /// </summary>
        /// <param name="timePeriods">周计划列表</param>
        /// <param name="enable">全局使能标志（顶层 enable 字段）</param>
        /// <returns>JSON 字符串</returns>
        public static string ToJsonWithDefaults(List<TimePeriod> timePeriods, bool enable = true)
        {
            var weekPlanItems = new List<object>();

            foreach (var tp in timePeriods)
            {
                if (!WeekNameMap.TryGetValue(tp.Week, out string? weekName))
                    continue; // 无效星期，跳过

                // 提取所有时间段（共8个），将空字符串转为 null 以便后续判断
                var timeSlots = new (string from, string to)[]
                {
                (tp.FromTime1, tp.ToTime1), (tp.FromTime2, tp.ToTime2),
                (tp.FromTime3, tp.ToTime3), (tp.FromTime4, tp.ToTime4),
                (tp.FromTime5, tp.ToTime5), (tp.FromTime6, tp.ToTime6),
                (tp.FromTime7, tp.ToTime7), (tp.FromTime8, tp.ToTime8)
                };

                for (int id = 1; id <= timeSlots.Length; id++)
                {
                    var (from, to) = timeSlots[id - 1];
                    bool isSet = !string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to);

                    // 确定最终的时间值
                    string beginTime, endTime;
                    if (isSet)
                    {
                        // 将 "HH:mm" 补全为 "HH:mm:ss"
                        beginTime = from.Length == 5 ? from + ":00" : from;
                        endTime = to.Length == 5 ? to + ":00" : to;
                    }
                    else
                    {
                        // 未设置，使用默认值
                        beginTime = DefaultTime;
                        endTime = DefaultTime;
                    }

                    // 构建匿名对象，enable 为 true 仅当时间段有效且 isSet 为 true
                    var item = new
                    {
                        week = weekName,
                        id = id,
                        enable = isSet,   // 只有有效时间段才启用，否则 false
                        TimeSegment = new
                        {
                            beginTime = beginTime,
                            endTime = endTime
                        }
                    };
                    weekPlanItems.Add(item);
                }
            }

            // 构建顶层匿名对象
            var root = new
            {
                UserRightWeekPlanCfg = new
                {
                    enable = enable,
                    WeekPlanCfg = weekPlanItems
                }
            };

            // 序列化（此处使用 Newtonsoft.Json，如需 System.Text.Json 请注释掉这一行，改用下面一行）
            string json = JsonConvert.SerializeObject(root, Formatting.Indented);
            // string json = System.Text.Json.JsonSerializer.Serialize(root, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            return json;
        }



        public static string ConvertDoorDataToWeekPlanJson(DoorData door)
        {
            if (door == null) return string.Empty;

            var weekNames = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

            // 空值返回 null，不再返回 ""
            Func<string, string> toSeconds = time =>
            {
                if (string.IsNullOrEmpty(time)) return null;

                if (time.Length == 8 && time[2] == ':' && time[5] == ':')
                    return time;

                return string.Join(":", time.Split(':').Select((p, i) => i == 0 ? p.PadLeft(2, '0') : p)) + ":00";
            };

            // 不再 Where 过滤，保留全部 8 个时段
            var validSlots = new[]
            {
        (id: 1, from: door.fromTime1, to: door.toTime1),
        (id: 2, from: door.fromTime2, to: door.toTime2),
        (id: 3, from: door.fromTime3, to: door.toTime3),
        (id: 4, from: door.fromTime4, to: door.toTime4),
        (id: 5, from: (string)null, to: (string)null),
        (id: 6, from: (string)null, to: (string)null),
        (id: 7, from: (string)null, to: (string)null),
        (id: 8, from: (string)null, to: (string)null)
    }
            .Select(slot => new
            {
                slot.id,
                from = toSeconds(slot.from),
                to = toSeconds(slot.to)
            })
            .ToList();

            var weekPlanItems = weekNames.SelectMany(week =>
                validSlots.Select(slot =>
                {
                    // from/to 为空时 enable = false
                    var enabled = !string.IsNullOrEmpty(slot.from) && !string.IsNullOrEmpty(slot.to);

                    return new
                    {
                        week = week,
                        id = slot.id,
                        enable = enabled,
                        TimeSegment = new
                        {
                            beginTime = enabled==false ?"00:00:00": slot.from,
                            endTime = enabled == false ? "00:10:00" : slot.to
                        },
                        authenticationTimesEnabled = false,
                        authenticationTimes = 0
                    };
                })
            ).ToList();

            var UserRightWeekPlanCfg = new
            {
                enable = true,
                WeekPlanCfg = weekPlanItems
            };

            var result = new
            {
                UserRightWeekPlanCfg = UserRightWeekPlanCfg
            };

            return JsonConvert.SerializeObject(result, Formatting.Indented);
        }
    }
}
