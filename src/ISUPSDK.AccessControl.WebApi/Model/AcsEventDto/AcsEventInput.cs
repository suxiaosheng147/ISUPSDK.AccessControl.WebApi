using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class SetAcsEvent
{
    /// <summary>
    /// 数据Id
    /// </summary>
    [DisplayName("数据Id")]
    [Required(ErrorMessage = "请输入{0}的值")]
    public Guid? Id { get; set; }


    /// <summary>
    /// 状态  1=待下发，2=下发失败 ，3=下发成功
    /// </summary>
    [DisplayName("状态")]
    [Required(ErrorMessage = "请输入{0}的值")]
    public int? State { get; set; }


    /// <summary>
    /// 数据
    /// </summary>
    [DisplayName("数据")]
    [Required(ErrorMessage = "请输入{0}的值")]
    public string Data { get; set; }


}
