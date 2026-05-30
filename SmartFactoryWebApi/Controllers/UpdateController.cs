using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    /// <summary>
    /// 客户端版本检查接口。
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UpdateController : ControllerBase
    {
        /// <summary>
        /// 计算客户端是否需要更新的业务服务。
        /// </summary>
        private readonly IAppUpdateService _appUpdateService;

        /// <summary>
        /// 初始化更新检查控制器依赖的业务服务。
        /// </summary>
        public UpdateController(IAppUpdateService appUpdateService)
        {
            _appUpdateService = appUpdateService;
        }

        /// <summary>
        /// 根据客户端当前版本返回更新信息。
        /// </summary>
        [HttpGet("check", Name = "update-check")]
        public async Task<ActionResult<Result<UpdateCheckResponse>>> Check(
            [FromQuery] string appId,
            [FromQuery] string platform,
            [FromQuery] int currentVersionCode,
            [FromQuery] string? channel,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return BadRequest(Result<UpdateCheckResponse>.Fail("appId is required."));
            }

            if (string.IsNullOrWhiteSpace(platform))
            {
                return BadRequest(Result<UpdateCheckResponse>.Fail("platform is required."));
            }

            if (currentVersionCode < 0)
            {
                return BadRequest(Result<UpdateCheckResponse>.Fail("currentVersionCode must be >= 0."));
            }

            var request = new CheckUpdateRequest
            {
                AppId = appId,
                Platform = platform,
                CurrentVersionCode = currentVersionCode,
                Channel = channel
            };

            var result = await _appUpdateService.CheckAsync(request, cancellationToken);
            if (!result.Success)
            {
                return NotFound(result);
            }

            return Ok(result);
        }
    }
}
