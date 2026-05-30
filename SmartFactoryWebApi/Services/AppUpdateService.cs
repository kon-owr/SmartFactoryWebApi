using Microsoft.Extensions.Options;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Options;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 按配置中心中的发布列表计算客户端是否需要升级。
    /// </summary>
    public class AppUpdateService : IAppUpdateService
    {
        /// <summary>
        /// 监听应用更新配置，支持运行期读取当前发布列表。
        /// </summary>
        private readonly IOptionsMonitor<AppUpdateOptions> _optionsMonitor;

        /// <summary>
        /// 记录更新配置缺失或匹配失败的诊断日志。
        /// </summary>
        private readonly ILogger<AppUpdateService> _logger;

        /// <summary>
        /// 初始化应用更新服务依赖的配置监听器和日志器。
        /// </summary>
        public AppUpdateService(IOptionsMonitor<AppUpdateOptions> optionsMonitor, ILogger<AppUpdateService> logger)
        {
            _optionsMonitor = optionsMonitor;
            _logger = logger;
        }

        /// <summary>
        /// 根据客户端版本和发布通道选择最新发布项，并返回是否需要可选或强制更新。
        /// </summary>
        public Task<Result<UpdateCheckResponse>> CheckAsync(CheckUpdateRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return Task.FromResult(Result<UpdateCheckResponse>.Fail("request is required."));
            }

            if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.Platform))
            {
                return Task.FromResult(Result<UpdateCheckResponse>.Fail("appId and platform are required."));
            }

            if (request.CurrentVersionCode < 0)
            {
                return Task.FromResult(Result<UpdateCheckResponse>.Fail("currentVersionCode must be >= 0."));
            }

            var options = _optionsMonitor.CurrentValue;
            if (!options.Enabled)
            {
                var disabledResponse = new UpdateCheckResponse
                {
                    HasUpdate = false,
                    ForceUpdate = false
                };

                return Task.FromResult(Result<UpdateCheckResponse>.Ok(disabledResponse, "update service disabled."));
            }

            var channel = string.IsNullOrWhiteSpace(request.Channel)
                ? options.DefaultChannel
                : request.Channel!;

            // 只在 appId、平台和发布通道全部匹配的候选版本中选择最高版本。
            var matchedReleases = options.Releases
                .Where(x => string.Equals(x.AppId, request.AppId, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.Equals(x.Platform, request.Platform, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.Equals(x.Channel, channel, StringComparison.OrdinalIgnoreCase))
                .Where(x => x.VersionCode > 0)
                .ToList();

            if (matchedReleases.Count == 0)
            {
                _logger.LogWarning("No update release found for appId={AppId}, platform={Platform}, channel={Channel}", request.AppId, request.Platform, channel);
                return Task.FromResult(Result<UpdateCheckResponse>.Fail("No release found for specified app/platform/channel."));
            }

            var latest = matchedReleases
                .OrderByDescending(x => x.VersionCode)
                .First();

            var hasUpdate = latest.VersionCode > request.CurrentVersionCode;
            var forceUpdate = request.CurrentVersionCode < latest.MinSupportedVersionCode
                || (hasUpdate && latest.IsMandatory);

            var response = new UpdateCheckResponse
            {
                HasUpdate = hasUpdate,
                ForceUpdate = forceUpdate,
                LatestVersionName = latest.VersionName,
                LatestVersionCode = latest.VersionCode,
                MinSupportedVersionCode = latest.MinSupportedVersionCode,
                DownloadUrl = hasUpdate ? latest.DownloadUrl : string.Empty,
                Sha256 = hasUpdate ? latest.Sha256 : string.Empty,
                ReleaseNotes = hasUpdate ? latest.ReleaseNotes : null,
                PublishedAt = latest.PublishedAt
            };

            return Task.FromResult(Result<UpdateCheckResponse>.Ok(response, "ok"));
        }
    }
}
