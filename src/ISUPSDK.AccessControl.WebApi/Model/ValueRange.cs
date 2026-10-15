using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ISUPSDK.AccessControl.WebApi.Model
{
    /// <summary>
    /// 值范围
    /// </summary>
    public class ValueRange
    {
        /// <summary>
        /// 最小值
        /// </summary>
        [JsonPropertyName("@min")]

        public int Min { get; set; }

        /// <summary>
        /// 最大值
        /// </summary>
        [JsonPropertyName("@max")]
        public int Max { get; set; }

    }
}
