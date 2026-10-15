using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// 获取
/// </summary>
public class GetAcsEventModel
{
    /// <summary>
    /// 
    /// </summary>

    public Guid Id { get; set; }


    /// <summary>
    /// 机器id
    /// </summary>
    public int? MachineId { get; set; }

    /// <summary>
    /// 机器编码
    /// </summary>
    public string MachineSN { get; set; }


    /// <summary>
    ///  1=待下发，2=下发失败 ，3=下发成功
    /// </summary>
    public int? State { get; set; }



    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime EndTime { get; set; }


    /// <summary>
    /// 工号  ，（01：学生，02：家长，03：职工）+人员Id
    /// </summary>
    public string EmployeeNoString { get; set; }

    /// <summary>
    ///  {"All",0}, {"Alarm",1},{"Exception",2},{ "Operation",3},{ "Event",5}
    /// </summary>
    public int? Major { get; set; } = 5;


    /// <summary>
    ///  {"All",0},
    /// </summary>
    public int? Minor { get; set; } = 0;
}