using SmartFactoryWebApi.DTO;

namespace SmartFactoryWebApi.Services
{
    public interface IAppUpdateService
    {
        Task<Result<UpdateCheckResponse>> CheckAsync(CheckUpdateRequest request, CancellationToken cancellationToken = default);
    }
}
