using System.Threading.Tasks;
using SmartFactoryWebApi.DTO;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 供服务层向前端广播感应料架回调结果。
    /// </summary>
    public interface IInductionHubContext
    {
        /// <summary>
        /// 通知前端感应入库回调结果。
        /// </summary>
        Task NotifyDepositCallbackAsync(DepositCallbackMessage message);

        /// <summary>
        /// 通知前端感应拣货回调结果。
        /// </summary>
        Task NotifyPickCallbackAsync(PickCallbackMessage message);
    }
}
