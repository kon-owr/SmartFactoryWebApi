using System.Threading;
using System.Threading.Tasks;
using SmartFactoryWebApi.DTO;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 定义客户端版本检查服务。
    /// </summary>
    public interface IAppUpdateService
    {
        /// <summary>
        /// 根据客户端版本信息返回是否需要更新。
        /// </summary>
        Task<Result<UpdateCheckResponse>> CheckAsync(CheckUpdateRequest request, CancellationToken cancellationToken = default);
    }
}
