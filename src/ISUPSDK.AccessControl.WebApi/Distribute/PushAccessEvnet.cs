using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    internal class PushAccessEvnet
    {
        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="data">数据</param>
        ///// <param name="settings">配置模型</param>
        ///// <param name="type">0=使用 settings.PushAcsPicture 的目录来存储图片 ；1 使用settings.AcsPicture  的目录来存储图片 </param>
        ///// <returns></returns>
        //public static async Task<bool> Send(Events data, Settings settings, int type = 0)
        //{
        //    if (string.IsNullOrWhiteSpace(settings.MonitorEventURL))
        //    {
        //        settings.PushAccessType = 1;
        //    }
        //    bool Success = false;
        //    try
        //    {
        //        switch (settings.PushAccessType)
        //        {
        //            case 0:

        //                string r = await HttpHelper.PostWithRetryAsync(settings.MonitorEventURL, data);
        //                if (!string.IsNullOrWhiteSpace(r))
        //                {
        //                    UpdateData<string> updateData = JsonHepler.FromJson<UpdateData<string>>(r);
        //                    if (updateData == null)
        //                    {
        //                        Success = false;
        //                    }

        //                    if (updateData != null)
        //                    {
        //                        Success = updateData.Status;

        //                        //判断返回的 是不是 
        //                        switch (updateData.ErrType)
        //                        {
        //                            case 409:
        //                                //删除图片
        //                                try
        //                                {
        //                                    if (!string.IsNullOrWhiteSpace(data.data.ExtEventPicture))
        //                                    {
        //                                        File.Delete(data.data.ExtEventPicture);

        //                                    }

        //                                }
        //                                catch (Exception)
        //                                {

        //                                    //throw;
        //                                }

        //                                break;


        //                            default:
        //                                break;
        //                        }

        //                    }
        //                }

        //                break;

        //            case 1:
        //                //保存图片 ，图片格式为 机器编号_用户类型_人员编号_开门时间（yyyyMMddHHmmss）.jpg
        //                //保存图片 ，图片格式为 (用户类型+用户ID-机器编号)_（yyyyMMddHHmmss）.jpg

        //                //string picturePath=settings.AcsPicture+$"\\{data.srcParentIndex.ToString()}_{data.data.UserType}_{data.data.ExtEventPersonNo}_{data.data.Time}.jpg";
        //                string path = string.Empty;
        //                string datePath = DateTime.Today.ToString("yyyyMMdd");
        //                string sn = data.srcParentIndex.ToString();
        //                if (type == 0)// settings.PushAcsPicture 的目录来存储图片
        //                {
        //                    path += settings.PushAcsPicture + sn + "\\" + datePath;
        //                }
        //                else if (type == 1) //使用settings.AcsPicture  的目录来存储图片
        //                {
        //                    //3041439-188_99_0_20260519140613249     (用户类型（10，20，30）人员编号-机器编号_?_0_yyyyMMddHHmmss.jpg )
        //                    path += settings.AcsPicture + sn + "\\" + datePath;
        //                }
        //                CopyWithOriginalName(data.data.ExtEventPicture, path);
        //                Success = true;
        //                break;
        //            default:
        //                break;
        //        }
        //        if (!Success)
        //        {
        //            settings.PushAccessType = 1;
        //            Success = await Send(data, settings, type);
        //        }
        //        return Success;
        //    }
        //    catch (Exception)
        //    {
        //        string json = JsonHepler.ToJson(data);
        //        //LogHelper.Error($"保存失败！数据：{json}");
        //        return Success;
        //    }
        //}

        /// <summary>
        /// 将文件复制到指定目录，并保留原文件名。
        /// </summary>
        /// <param name="sourceFilePath">源文件完整路径（必须包含文件名）</param>
        /// <param name="destDirectory">目标目录路径（必须为目录，不以文件结尾）</param>
        /// <param name="overwrite">是否覆盖已存在的文件</param>
        /// <exception cref="FileNotFoundException">源文件不存在或源路径指向目录</exception>
        /// <exception cref="DirectoryNotFoundException">目标目录的父级路径无效（如驱动器不存在）</exception>
        /// <exception cref="ArgumentException">源路径或目标目录格式无效</exception>
        public static void CopyWithOriginalName(string sourceFilePath, string destDirectory, bool overwrite = false)
        {
            // 1. 校验源文件是否存在，同时排除传入目录的可能
            if (!File.Exists(sourceFilePath))
            {
                // 如果路径存在但是目录，File.Exists 返回 false，这里给出更明确的提示
                if (Directory.Exists(sourceFilePath))
                    throw new FileNotFoundException($"源路径指向的是目录，而非文件: {sourceFilePath}");
                else
                    throw new FileNotFoundException($"源文件不存在: {sourceFilePath}");
            }

            // 2. 获取原文件名，并确保非空
            string fileName = Path.GetFileName(sourceFilePath);
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException($"源路径无效，未能提取到文件名: {sourceFilePath}", nameof(sourceFilePath));

            // 3. 组合目标完整路径，同时去除 destDirectory 末尾多余的分隔符
            destDirectory = destDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            ?? throw new ArgumentNullException(nameof(destDirectory));
            string destFilePath = Path.Combine(destDirectory, fileName);

            // 4. 确保目标目录存在
            if (!Directory.Exists(destDirectory))
            {
                try
                {
                    Directory.CreateDirectory(destDirectory);
                }
                catch (Exception ex)
                {
                    throw new DirectoryNotFoundException($"无法创建目标目录: {destDirectory}", ex);
                }
            }

            // 5. 复制文件
            File.Copy(sourceFilePath, destFilePath, overwrite);
        }
    }
}
