using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    /// <summary>
    /// 普通拣货流程接口。
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PickController : ControllerBase
    {
        /// <summary>
        /// 记录普通拣货接口运行过程中的诊断日志。
        /// </summary>
        private readonly ILogger<PickController> _logger;

        /// <summary>
        /// 处理普通拣货查询、锁定、解锁和完成的业务服务。
        /// </summary>
        private readonly IPickDetailService _pickDetailService;

        /// <summary>
        /// 初始化普通拣货控制器依赖的日志和业务服务。
        /// </summary>
        public PickController(ILogger<PickController> logger, IPickDetailService pickDetailService)
        {
            _logger = logger;
            _pickDetailService = pickDetailService;
        }

        /// <summary>
        /// 校验领料单是否存在。
        /// </summary>
        [HttpPost("exists", Name = "pick-exists")]
        public async Task<ActionResult<Result<bool>>> IsPickApplyExists([FromBody] GetDocNoRequest getDocNoRequest)
        {
            if (getDocNoRequest == null || string.IsNullOrWhiteSpace(getDocNoRequest.DocNo))
            {
                return BadRequest(Result<bool>.Fail("领料单不存在"));
            }

            var exists = await _pickDetailService.isExistPickApplyDoc(getDocNoRequest.DocNo);
            return Ok(exists);
        }

        /// <summary>
        /// 查询领料单明细。
        /// </summary>
        [HttpPost("details", Name = "pick-details")]
        public async Task<ActionResult<Result<List<PickDetail>>>> GetPickDetails([FromBody] GetDocNoRequest getDocNoRequest)
        {
            if (getDocNoRequest == null || string.IsNullOrWhiteSpace(getDocNoRequest.DocNo))
            {
                return BadRequest(Result<List<PickDetail>>.Fail("领料单不存在"));
            }

            var pickDetails = await _pickDetailService.GetPickDetailAsync(getDocNoRequest.DocNo);
            return Ok(pickDetails);
        }

        /// <summary>
        /// 查询指定领料单在当前仓库下的锁定条码。
        /// </summary>
        [HttpPost("lockedbarcode", Name = "pick-lockedbarcode")]
        public async Task<ActionResult<Result<List<LockedBarNo>>>> GetLockedBarCode([FromBody] PickReserveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DocNo))
            {
                return BadRequest(Result<List<LockedBarNo>>.Fail("领料单号不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.WarehouseLocation))
            {
                return BadRequest(Result<List<LockedBarNo>>.Fail("仓库编码不能为空"));
            }

            var lockedBarCode = await _pickDetailService.GetLockedBarNoByDocNosAsync(request.DocNo, request.WarehouseLocation);
            return Ok(lockedBarCode);
        }

        /// <summary>
        /// 根据物料 GUID 查询料号。
        /// </summary>
        [HttpPost("itemno", Name = "pick-itemno")]
        public async Task<ActionResult<Result<string>>> GetItemNo([FromBody] GetItemGuidRequest getItemGuidRequest)
        {
            if (getItemGuidRequest == null || string.IsNullOrWhiteSpace(getItemGuidRequest.ItemGuid))
            {
                return BadRequest(Result<string>.Fail("物料标识不能为空"));
            }

            var itemNo = await _pickDetailService.GetItemNoByItemGuid(getItemGuidRequest.ItemGuid);
            return Ok(itemNo);
        }

        /// <summary>
        /// 按需求数量和仓库执行 FIFO 条码查询。
        /// </summary>
        [HttpPost("bars", Name = "pick-bars")]
        public async Task<ActionResult<Result<List<BarDetail>>>> GetBars([FromBody] GetBarsRequest request)
        {
            if (request == null)
            {
                return BadRequest(Result<List<BarDetail>>.Fail("请求为空"));
            }

            if (string.IsNullOrWhiteSpace(request.ItemGuid))
            {
                return BadRequest(Result<List<BarDetail>>.Fail("料号不能为空"));
            }

            var bars = await _pickDetailService.GetBarByRequiredQtyQtyAndProductNo(
                request.RequiredQty,
                request.ItemGuid,
                request.WarehouseLocation);

            return Ok(bars);
        }

        /// <summary>
        /// 根据领料单查询并锁定当前仓库可用条码。
        /// </summary>
        [HttpPost("reserve", Name = "pick-reserve")]
        public async Task<ActionResult<Result<List<VariableItem>>>> Reserve([FromBody] PickReserveRequest request)
        {
            if (request == null)
            {
                return BadRequest(Result<List<VariableItem>>.Fail("请求为空"));
            }

            if (string.IsNullOrWhiteSpace(request.DocNo))
            {
                return BadRequest(Result<List<VariableItem>>.Fail("领料单号不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.WarehouseLocation))
            {
                return BadRequest(Result<List<VariableItem>>.Fail("仓库编码不能为空"));
            }

            var result = await _pickDetailService.ReserveBarsByDocNoAsync(request.DocNo, request.WarehouseLocation);
            return Ok(result);
        }

        /// <summary>
        /// 显式锁定一批条码。
        /// </summary>
        [HttpPost("lock", Name = "pick-lock")]
        public async Task<ActionResult<Result<bool>>> LockBars([FromBody] LockBarsRequest request)
        {
            if (request == null)
            {
                return BadRequest(Result<bool>.Fail("请求为空"));
            }

            if (request.BarNolist == null)
            {
                return BadRequest(Result<bool>.Fail("条码列表不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.WarehouseLocation))
            {
                return BadRequest(Result<bool>.Fail("仓库编码不能为空"));
            }

            var result = await _pickDetailService.LockBarsAsync(request.BarNolist, request.DocNo, request.WarehouseLocation);
            return Ok(result);
        }

        /// <summary>
        /// 显式释放一批条码的锁定。
        /// </summary>
        [HttpPost("unlock", Name = "pick-unlock")]
        public async Task<ActionResult<Result<bool>>> UnLockBars([FromBody] LockBarsRequest request)
        {
            if (request == null)
            {
                return BadRequest(Result<bool>.Fail("请求为空"));
            }

            if (request.BarNolist == null || request.BarNolist.Count == 0)
            {
                return BadRequest(Result<bool>.Fail("条码列表不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.DocNo))
            {
                return BadRequest(Result<bool>.Fail("领料单号不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.WarehouseLocation))
            {
                return BadRequest(Result<bool>.Fail("仓库编码不能为空"));
            }

            var result = await _pickDetailService.UnLockBarsAsync(request.BarNolist, request.DocNo, request.WarehouseLocation);
            return Ok(result);
        }

        /// <summary>
        /// 提交拣货完成结果，并释放库位和锁定记录。
        /// </summary>
        [HttpPost("complete", Name = "pick-complete")]
        public async Task<ActionResult<Result<bool>>> CompletePicking([FromBody] CompletePickingRequest request)
        {
            if (request == null)
            {
                return BadRequest(Result<bool>.Fail("请求为空"));
            }

            if (string.IsNullOrWhiteSpace(request.DocNo))
            {
                return BadRequest(Result<bool>.Fail("领料单号不能为空"));
            }

            if (request.BinNos == null || request.BinNos.Count == 0)
            {
                return BadRequest(Result<bool>.Fail("库位列表不能为空"));
            }

            if (string.IsNullOrWhiteSpace(request.WarehouseLocation))
            {
                return BadRequest(Result<bool>.Fail("仓库编码不能为空"));
            }

            var result = await _pickDetailService.CompletePickingAsync(request.DocNo, request.BinNos, request.WarehouseLocation);
            return Ok(result);
        }
    }
}
