using Microsoft.AspNetCore.SignalR;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Services;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Hubs
{
    /// <summary>
    /// 感应料架回调广播使用的 SignalR Hub。
    /// </summary>
    public class InductionHub : Hub
    {
        /// <summary>
        /// 入库回调广播事件名，客户端订阅后接收入库成功或失败结果。
        /// </summary>
        public const string DepositCallbackEvent = "ReceiveDepositCallback";

        /// <summary>
        /// 拣货回调广播事件名，客户端订阅后接收出库成功、失败或非法取货结果。
        /// </summary>
        public const string PickCallbackEvent = "ReceivePickCallback";
    }

    /// <summary>
    /// 感应料架 Hub 的广播封装，供服务层直接推送结果。
    /// </summary>
    public class InductionHubContext : IInductionHubContext
    {
        /// <summary>
        /// SignalR Hub 上下文，用于从服务层向所有已连接客户端广播消息。
        /// </summary>
        private readonly IHubContext<InductionHub> _hubContext;

        /// <summary>
        /// 初始化 Hub 广播封装并保存 SignalR 上下文。
        /// </summary>
        public InductionHubContext(IHubContext<InductionHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// 将感应入库回调结果广播给前端入库页面。
        /// </summary>
        public async Task NotifyDepositCallbackAsync(DepositCallbackMessage message)
        {
            await _hubContext.Clients.All.SendAsync(InductionHub.DepositCallbackEvent, message);
        }

        /// <summary>
        /// 将感应拣货回调结果广播给前端拣货页面。
        /// </summary>
        public async Task NotifyPickCallbackAsync(PickCallbackMessage message)
        {
            await _hubContext.Clients.All.SendAsync(InductionHub.PickCallbackEvent, message);
        }
    }
}
