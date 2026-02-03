using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PickController : ControllerBase
    {
        private readonly ILogger<PickController> _logger;
        private readonly IPickDetailService _pickDetailService;

        public PickController(ILogger<PickController> logger, IPickDetailService pickDetailService)
        {
            _logger = logger;
            _pickDetailService = pickDetailService;
        }

        // 领料单是否存在：POST /api/pick/exists
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

        // 获取领料单明细：POST /api/pick/details
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

        // 获取领料单明细：POST /api/pick/details
        [HttpPost("lockedbarcode", Name = "pick-lockedbarcode")]
        public async Task<ActionResult<Result<List<LockedBarNo>>>> GetLockedBarCode([FromBody] GetDocNoRequest getDocNoRequest)
        {
            if (getDocNoRequest == null || string.IsNullOrWhiteSpace(getDocNoRequest.DocNo))
            {
                return BadRequest("docno is required.");
            }

            var lockedBarCode = await _pickDetailService.GetLockedBarNoByDocNosAsync(getDocNoRequest.DocNo);
            return Ok(lockedBarCode);
        }

        // 获取领料单明细：POST /api/pick/details
        [HttpPost("itemno", Name = "pick-itemno")]
        public async Task<ActionResult<Result<string>>> GetItemNo([FromBody] GetItemGuidRequest getItemGuidRequest)
        {
            if (getItemGuidRequest == null || string.IsNullOrWhiteSpace(getItemGuidRequest.ItemGuid))
            {
                return BadRequest("docno is required.");
            }

            var itemNo = await _pickDetailService.GetItemNoByItemGuid(getItemGuidRequest.ItemGuid);
            return Ok(itemNo);
        }

        // 获取条码列表：POST /api/pick/bars
        [HttpPost("bars", Name = "pick-bars")]
        public async Task<ActionResult<Result<List<BarDetail>>>> GetBars([FromBody] GetBarsRequest request)
        {
            if (request == null)
            {
                return BadRequest(Result<Result<List<BarDetail>>>.Fail("请求为空"));
            }

            if (string.IsNullOrWhiteSpace(request.ItemGuid))
            {
                return BadRequest(Result<Result<List<BarDetail>>>.Fail("料号不能为空"));
            }

            // 调用 Service 方法 (假设 Service 方法名为 GetBarByRequiredQtyQtyAndProductNo)
            var bars = await _pickDetailService.GetBarByRequiredQtyQtyAndProductNo(
                request.RequiredQty, 
                request.ItemGuid, 
                request.WarehouseLocation
            );

            return Ok(bars);
        }

        // 获取条码列表：POST /api/pick/bars
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

            // 调用 Service 方法 (假设 Service 方法名为 GetBarByRequiredQtyQtyAndProductNo)
            var result = await _pickDetailService.LockBarsAsync(request.BarNolist, request.DocNo);


            return Ok(result);
        }

        // 获取条码列表：POST /api/pick/bars
        [HttpPost("unlock", Name = "pick-unlock")]
        public async Task<ActionResult<Result<bool>>> UnLockBars([FromBody] LockBarsRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is null.");
            }

            if (request.BarNolist == null)
            {
                return BadRequest("ItemGuid is required.");
            }

            // 调用 Service 方法 (假设 Service 方法名为 GetBarByRequiredQtyQtyAndProductNo)
            await _pickDetailService.UnLockBarsAsync(request.BarNolist);

            return Ok();
        }
    }
}
