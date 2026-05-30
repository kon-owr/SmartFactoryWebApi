using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    /// <summary>
    /// 提供感应料架入库、拣货、亮灯和设备回调的 HTTP 接口。
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class InductionController : ControllerBase
    {
        /// <summary>
        /// 处理感应入库的料架验证、入库请求、取消和回调落库。
        /// </summary>
        private readonly IInductionEntryService _entryService;

        /// <summary>
        /// 处理感应拣货的料号查询、启动、取消和回调落库。
        /// </summary>
        private readonly IInductionPickService _pickService;

        /// <summary>
        /// 处理感应料架标签和空库位亮灯请求。
        /// </summary>
        private readonly IInductionLightService _lightService;

        /// <summary>
        /// 初始化感应控制器依赖的入库、拣货和亮灯服务。
        /// </summary>
        public InductionController(
            IInductionEntryService entryService,
            IInductionPickService pickService,
            IInductionLightService lightService)
        {
            _entryService = entryService;
            _pickService = pickService;
            _lightService = lightService;
        }

        /// <summary>
        /// 验证入库料架是否属于当前仓库并返回可用空库位数量。
        /// </summary>
        [HttpPost("entry/validate-shelf")]
        public async Task<ActionResult<Result<InductionShelfValidation>>> ValidateShelf([FromBody] InductionShelfValidateRequest request)
        {
            var result = await _entryService.ValidateShelfAsync(request.ShelfCode, request.WarehouseLocation);
            return Ok(result);
        }

        /// <summary>
        /// 向感应料架提交条码入库请求，成功后等待设备回调。
        /// </summary>
        [HttpPost("entry/deposit")]
        public async Task<ActionResult<Result<string>>> Deposit([FromBody] InductionDepositRequest request)
        {
            var result = await _entryService.DepositAsync(request.Barcode, request.ShelfCode, request.WarehouseLocation);
            return Ok(result);
        }

        /// <summary>
        /// 取消尚未完成回调的感应入库请求。
        /// </summary>
        [HttpPost("entry/cancel")]
        public async Task<ActionResult<Result<string>>> CancelDeposit([FromBody] InductionCancelRequest request)
        {
            var result = await _entryService.CancelDepositAsync(request.Barcode);
            return Ok(result);
        }

        /// <summary>
        /// 接收感应料架入库回调并交由服务层更新库存和广播前端。
        /// </summary>
        [HttpPost("entry/callback")]
        public async Task<ActionResult<VendorCallbackResponse>> DepositCallback([FromBody] InductionDepositCallbackRequest request)
        {
            var result = await _entryService.HandleDepositCallbackAsync(request.LabelId, request.Location, request.DetailsJson);
            return Ok(ToVendorCallbackResponse(result));
        }

        /// <summary>
        /// 根据料号关键字返回当前仓库可感应拣货的料号候选。
        /// </summary>
        [HttpPost("pick/item-suggestions")]
        public async Task<ActionResult<Result<List<string>>>> GetItemSuggestions([FromBody] InductionPickSuggestionRequest request)
        {
            var result = await _pickService.GetItemSuggestionsAsync(request.Keyword, request.WarehouseLocation, request.Limit);
            return Ok(result);
        }

        /// <summary>
        /// 查询料号对应的可拣条码，并在成功时触发预览亮灯。
        /// </summary>
        [HttpPost("pick/query")]
        public async Task<ActionResult<Result<List<InductionPickItem>>>> QueryPick([FromBody] InductionPickQueryRequest request)
        {
            var result = await _pickService.QueryByItemNoAsync(request.ItemNo, request.RequiredQty, request.WarehouseLocation);
            if (!result.Success || result.Data == null || result.Data.Count == 0)
            {
                return Ok(result);
            }

            var labelIds = result.Data.Select(item => item.BarNo).ToList();
            var lightResult = await _lightService.LightUpLabelAsync(labelIds, request.Color, 1);
            if (IsRackApiFailure(lightResult))
            {
                return Ok(Result<List<InductionPickItem>>.Fail(lightResult));
            }

            return Ok(result);
        }

        /// <summary>
        /// 启动感应拣货请求，使料架进入等待取货回调状态。
        /// </summary>
        [HttpPost("pick/start")]
        public async Task<ActionResult<Result<string>>> StartPick([FromBody] InductionPickStartRequest request)
        {
            var result = await _pickService.StartPickAsync(request.LabelIds, request.WarehouseLocation, request.Color);
            return Ok(result);
        }

        /// <summary>
        /// 取消感应拣货请求并释放仍在等待的标签。
        /// </summary>
        [HttpPost("pick/cancel")]
        public async Task<ActionResult<Result<string>>> CancelPick([FromBody] InductionPickCancelRequest request)
        {
            var result = await _pickService.CancelPickAsync(request.LabelIds);
            return Ok(result);
        }

        /// <summary>
        /// 接收感应料架拣货回调并交由服务层更新状态和广播前端。
        /// </summary>
        [HttpPost("pick/callback")]
        public async Task<ActionResult<VendorCallbackResponse>> PickCallback([FromBody] InductionPickCallbackRequest request)
        {
            var result = await _pickService.HandlePickCallbackAsync(request.LabelId, request.Location, request.DetailsJson);
            return Ok(ToVendorCallbackResponse(result));
        }

        /// <summary>
        /// 点亮指定料架的所有空库位，用于引导感应入库。
        /// </summary>
        [HttpPost("light/empty-locations")]
        public async Task<ActionResult<Result<string>>> LightOnEmptyLocations([FromBody] InductionLightRequest request)
        {
            var result = await _lightService.LightOnAllEmptyLocationAsync(request.ShelfCode, request.Color);
            return Ok(IsRackApiFailure(result)
                ? Result<string>.Fail(result)
                : Result<string>.Ok(result, "亮灯成功"));
        }

        /// <summary>
        /// 熄灭指定料架的所有空库位灯光。
        /// </summary>
        [HttpPost("light/off-empty-locations")]
        public async Task<ActionResult<Result<string>>> LightOffEmptyLocations([FromBody] InductionLightRequest request)
        {
            var result = await _lightService.LightOnAllEmptyLocationAsync(request.ShelfCode, 0);
            return Ok(IsRackApiFailure(result)
                ? Result<string>.Fail(result)
                : Result<string>.Ok(result, "熄灯成功"));
        }

        /// <summary>
        /// 判断料架 API 的文本响应是否表示失败或超时，用于统一包装 Result。
        /// </summary>
        private static bool IsRackApiFailure(string? result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return true;
            }

            return result.Contains("失败", StringComparison.OrdinalIgnoreCase)
                || result.Contains("超时", StringComparison.OrdinalIgnoreCase)
                || result.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || result.Contains("error", StringComparison.OrdinalIgnoreCase)
                || result.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        private static VendorCallbackResponse ToVendorCallbackResponse(Result<string> result)
        {
            return new VendorCallbackResponse
            {
                Success = result.Success,
                Message = result.Message ?? string.Empty,
                Data = result.Data
            };
        }
    }
}
