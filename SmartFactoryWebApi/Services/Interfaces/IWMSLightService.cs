using SmartFactoryWebApi.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 封装普通料架亮灯服务的调用契约。
    /// </summary>
    public interface IWMSLightService
    {
        /// <summary>
        /// 按库位列表批量切换灯光状态。
        /// </summary>
        Task<string> ChangeBinNoLightStatus(List<string> binNoList, LightColorCode lightColorCode, CancellationToken cancellationToken = default);
    }
}
