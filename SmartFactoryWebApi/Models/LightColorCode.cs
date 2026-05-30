using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    public enum LightColorCode
    {
        [Description("熄灯")]
        Grey = 0,
        [Description("红色")]
        Red = 1,
        [Description("绿色")]
        Green = 2,
        [Description("蓝色")]
        Blue = 3,
        [Description("黄色")]
        Yellow = 4,
        [Description("粉色")]
        Pink = 5,
        [Description("青色")]
        Cyan = 6,
        [Description("白色")]
        White = 7,
    }
}
