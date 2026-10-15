using Experimental.System.Messaging;
namespace ISUPSDK.AccessControl.WebApi.Methods
{
    /// <summary>
    /// 消息队列帮助类
    /// </summary>
    public class MessageQueueHepler
    {
        /// <summary>
        /// 创建队列
        /// </summary>
        /// <param name="msgPath"></param>
        /// <returns></returns>
        public static MessageQueue Create(string? msgPath)
        {
            if (!MessageQueue.Exists(msgPath))
                return MessageQueue.Create(msgPath);
            else
                return new MessageQueue(msgPath);
        }
        /// <summary>
        /// 删除队列
        /// </summary>
        /// <param name="msgPath"></param>
        public static void Delete(string msgPath)
        {
            if (!MessageQueue.Exists(msgPath))
                MessageQueue.Delete(msgPath);
        }
        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="msgPath"></param>
        /// <param name="msg"></param>
        public static void Send(string msgPath, string msg)
        {
            using MessageQueue mq = new(msgPath);
            mq.Send(msg);
        }
        /// <summary>
        /// 发送（事务）
        /// </summary>
        /// <param name="msgPath"></param>
        /// <param name="msg"></param>
        public static void SendTran(string msgPath, string msg)
        {
            using MessageQueue mq = new(msgPath);
            mq.Send(msg, MessageQueueTransactionType.Single);
        }
        /// <summary>
        /// 获取
        /// </summary>
        /// <param name="msgPath"></param>
        /// <returns></returns>
        public static string? Get(string msgPath)
        {
            using MessageQueue mq = new(msgPath);
            if (mq.CanRead)
            {
                if (mq.GetAllMessages().Length == 0)
                    return null;
                else
                {
                    var m = mq.Receive();
                    if (m != null)
                    {
                        m.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });
                        return m.Body.ToString();
                    }
                    else
                        return null;
                }
            }
            else
                return null;
        }
    }
}
