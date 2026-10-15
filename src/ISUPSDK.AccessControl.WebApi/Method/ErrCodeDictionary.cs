using System.Collections.Generic;

namespace ISUPSDK.AccessControl.WebApi
{
    public static class ErrCodeDictionary
    {
        static ErrCodeDictionary()
        {
            ErrCode = new Dictionary<int, string>
            {
                { 0, "没有错误" },
                { 1, "用户名密码错误" },
                { 2, "权限不足" },
                { 3, "没有初始化" },
                { 4, "没有初始化" },
                { 5, "连接到DVR的客户端个数超过最大" },
                { 6, "版本不匹配" },
                { 7, "连接服务器失败" },
                { 8, "向服务器发送失败" },
                { 9, "从服务器接收数据失败" },
                { 10, "从服务器接收数据超时" },
                { 11, "传送的数据有误" },
                { 12, "调用次序错误" },
                { 13, "无此权限" },
                { 14, "DVR命令执行超时c" },
                { 17, "参数错误" },
                { 23, "服务器不支持" },
                { 29, "DVR操作失败" },
                { 40, "路径错误" },
                { 41, "资源分配错误" },
                { 42, "声卡模式错误" },
                { 43, "缓冲区太小" },
                { 44, "创建SOCKET出错" },
                { 45, "设置SOCKET出错" },
                { 46, "个数达到最大" },
                { 47, "用户不存在" }
            };
        }

        /// <summary>
        /// 部分错误代码中文意思
        /// </summary>
        public static Dictionary<int, string> ErrCode { get; set; }
    }
}
