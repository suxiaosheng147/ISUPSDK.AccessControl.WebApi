using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ISUPSDK.AccessControl.WebApi.Model
{
    /// <summary>
    /// 用户权限-假期计划组配置
    /// </summary>
    public class UserRightHolidayGroupCfg
    {
        [JsonPropertyName("groupNo")]
        public ValueRange GroupNo { get; set; }

        [JsonPropertyName("enable")]
        public string Enable { get; set; }

        [JsonPropertyName("groupName")]
        public ValueRange GroupName { get; set; }

        [JsonPropertyName("holidayPlanNo")]
        public ValueRange HolidayPlanNo { get; set; }
    }

    
}
