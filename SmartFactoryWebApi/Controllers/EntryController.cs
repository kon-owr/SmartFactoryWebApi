using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    /// <summary>
    /// 普通入库流程接口。
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class EntryController : ControllerBase
    {
        /// <summary>
        /// 处理普通入库分配和提交的业务服务。
        /// </summary>
        private readonly IEntryDetailService _entryDetailService;

        /// <summary>
        /// 记录普通入库接口运行过程中的诊断日志。
        /// </summary>
        private readonly ILogger<EntryController> _logger;

        /// <summary>
        /// 初始化普通入库控制器依赖的日志和业务服务。
        /// </summary>
        public EntryController(ILogger<EntryController> logger, IEntryDetailService entryDetailService)
        {
            _logger = logger;
            _entryDetailService = entryDetailService;
        }

        /// <summary>
        /// 根据库位和扫码结果分配上架库位。
        /// </summary>
        [HttpPost("allocate", Name = "entry-allocate")]
        public async Task<ActionResult<Result<IEnumerable<PalletBarRelation>>>> Allocate([FromBody] GetAllocateRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var result = await _entryDetailService.AllocateAsync(request.barCode, request.binNo);
            return Ok(result);
        }

        /// <summary>
        /// 提交已经确认的入库结果。
        /// </summary>
        [HttpPost("commit", Name = "entry-commit")]
        public async Task<ActionResult<Result<IEnumerable<PalletBarRelation>>>> EnterWareHouse([FromBody] GetCommitBarDetails request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            var result = await _entryDetailService.CommitAsync(request.Items, request.WarehouseLocation);
            return Ok(result);
        }
    }
}
