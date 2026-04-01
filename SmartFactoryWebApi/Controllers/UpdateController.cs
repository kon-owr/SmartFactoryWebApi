using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UpdateController : ControllerBase
    {
        private readonly IAppUpdateService _appUpdateService;

        public UpdateController(IAppUpdateService appUpdateService)
        {
            _appUpdateService = appUpdateService;
        }

        // GET /api/update/check?appId=wmsapp&platform=android&currentVersionCode=100&channel=prod
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
