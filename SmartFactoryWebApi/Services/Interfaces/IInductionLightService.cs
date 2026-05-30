using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 面向业务层暴露的感应料架灯光控制接口。
    /// </summary>
    public interface IInductionLightService
    {
        /// <summary>
        /// 点亮指定料架的所有空库位。
        /// </summary>
        Task<string> LightOnAllEmptyLocationAsync(string shelfCode, int color);

        /// <summary>
        /// 点亮指定条码对应的库位，支持仅亮灯和真实出库两种模式。
        /// </summary>
        Task<string> LightUpLabelAsync(List<string> labelIds, int color, int outStockType);
    }
}
