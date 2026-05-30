using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;
using System.Text.Json;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 处理感应拣货的条码查询、启动出库、取消和回调事务。
    /// </summary>
    public class InductionPickService : IInductionPickService
    {
        /// <summary>
        /// 访问 WMS 条码、料号、库存和感应料架库位数据的数据库上下文。
        /// </summary>
        private readonly WmsDbContext _context;

        /// <summary>
        /// 调用外部感应料架设备接口的服务。
        /// </summary>
        private readonly IInductionRackApiService _inductionRackApi;

        /// <summary>
        /// 向前端广播感应拣货回调结果的 Hub 封装。
        /// </summary>
        private readonly IInductionHubContext _hubContext;

        /// <summary>
        /// 允许执行感应拣货的仓库编码集合。
        /// </summary>
        private readonly HashSet<string> _allowedWarehouses;

        /// <summary>
        /// 初始化感应拣货服务依赖，并读取允许仓库配置。
        /// </summary>
        public InductionPickService(
            WmsDbContext context,
            IInductionRackApiService inductionRackApi,
            IInductionHubContext hubContext,
            IConfiguration configuration)
        {
            _context = context;
            _inductionRackApi = inductionRackApi;
            _hubContext = hubContext;
            _allowedWarehouses = configuration
                .GetSection("InductionRack:AllowedWarehouses")
                .GetChildren()
                .Select(section => section.Value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 按料号和可选需求数量查询当前仓库感应料架上的可出库条码。
        /// </summary>
        public async Task<Result<List<InductionPickItem>>> QueryByItemNoAsync(string itemNo, decimal? requiredQty, string warehouseLocation)
        {
            if (string.IsNullOrWhiteSpace(itemNo))
            {
                return Result<List<InductionPickItem>>.Fail("料号不能为空。");
            }

            var warehouseValidation = ValidateWarehouseContext(warehouseLocation);
            if (warehouseValidation != null)
            {
                return Result<List<InductionPickItem>>.Fail(warehouseValidation);
            }

            var items = await BuildPickQuery(itemNo.Trim(), warehouseLocation).ToListAsync();
            if (items.Count == 0)
            {
                return Result<List<InductionPickItem>>.Fail($"料号 {itemNo} 在感应料架上没有可出库的条码。");
            }

            if (requiredQty.HasValue && requiredQty.Value > 0)
            {
                var selectedItems = new List<InductionPickItem>();
                decimal accumulatedQty = 0;

                foreach (var item in items)
                {
                    selectedItems.Add(item);
                    accumulatedQty += item.BarQty;
                    if (accumulatedQty >= requiredQty.Value)
                    {
                        break;
                    }
                }

                if (accumulatedQty < requiredQty.Value)
                {
                    return Result<List<InductionPickItem>>.Fail("货架上的物料不满足需求。");
                }

                items = selectedItems;
            }

            return Result<List<InductionPickItem>>.Ok(items, $"查询到 {items.Count} 个条码。");
        }

        /// <summary>
        /// 根据料号关键字返回当前仓库可出库条码对应的料号建议。
        /// </summary>
        public async Task<Result<List<string>>> GetItemSuggestionsAsync(string keyword, string warehouseLocation, int limit)
        {
            var warehouseValidation = ValidateWarehouseContext(warehouseLocation);
            if (warehouseValidation != null)
            {
                return Result<List<string>>.Fail(warehouseValidation);
            }

            var normalizedKeyword = keyword?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return Result<List<string>>.Ok(new List<string>(), "关键字为空。");
            }

            var takeCount = limit <= 0 ? 20 : Math.Min(limit, 20);
            var likeKeyword = $"%{normalizedKeyword}%";

            var items = await (from bar in _context.BarDetails
                               join item in _context.itemDetails on bar.ItemGuid equals item.Guid
                               join shelf in _context.ShelfDetails on bar.BinNo equals shelf.BinNo
                               where EF.Functions.Like(item.ItemNo, likeKeyword)
                                   && bar.IsRack == "Y"
                                   && bar.isDelete != "Y"
                                   && bar.EnableFlag == "Y"
                                   && (bar.BarQty ?? 0m) > 0m
                                   && shelf.IsInduction == "Y"
                                   && shelf.DeleteFlag == "N"
                                   && shelf.IsEnable == "N"
                                   && shelf.WarehouseNo == warehouseLocation
                               select item.ItemNo)
                .Distinct()
                .OrderBy(itemNo => itemNo)
                .Take(takeCount)
                .ToListAsync();

            return Result<List<string>>.Ok(items, $"查询到 {items.Count} 个料号建议。");
        }

        /// <summary>
        /// 校验标签仍在当前仓库感应料架后，向外部料架发送出库指令。
        /// </summary>
        public async Task<Result<string>> StartPickAsync(List<string> labelIds, string warehouseLocation, int color)
        {
            var warehouseValidation = ValidateWarehouseContext(warehouseLocation);
            if (warehouseValidation != null)
            {
                return Result<string>.Fail(warehouseValidation);
            }

            var normalizedLabelIds = labelIds
                .Where(labelId => !string.IsNullOrWhiteSpace(labelId))
                .Select(labelId => labelId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedLabelIds.Count == 0)
            {
                return Result<string>.Fail("条码列表不能为空。");
            }

            var validLabelIds = await (from bar in _context.BarDetails
                                       join shelf in _context.ShelfDetails on bar.BinNo equals shelf.BinNo
                                       where normalizedLabelIds.Contains(bar.BarNo!)
                                           && bar.IsRack == "Y"
                                           && bar.isDelete != "Y"
                                           && shelf.IsInduction == "Y"
                                           && shelf.DeleteFlag == "N"
                                           && shelf.IsEnable == "N"
                                           && shelf.WarehouseNo == warehouseLocation
                                       select bar.BarNo!)
                .Distinct()
                .ToListAsync();

            if (validLabelIds.Count != normalizedLabelIds.Count)
            {
                return Result<string>.Fail("部分条码已失效或不在当前感应仓库中。");
            }

            var detailsJson = JsonSerializer.Serialize(new InductionPickDetails
            {
                WarehouseLocation = warehouseLocation,
                OutStockType = 2,
                OperationTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
            });

            var result = await _inductionRackApi.LightUpLabelAsync(
                JsonSerializer.Serialize(normalizedLabelIds),
                color,
                2,
                detailsJson);

            if (IsRackApiFailure(result))
            {
                return Result<string>.Fail($"调用出库接口失败：{result}");
            }

            return Result<string>.Ok(result, "已发送出库请求，等待拣货回调。");
        }

        /// <summary>
        /// 取消指定标签的感应拣货状态，并将标签灯恢复为熄灭颜色。
        /// </summary>
        public async Task<Result<string>> CancelPickAsync(List<string> labelIds)
        {
            var normalizedLabelIds = labelIds
                .Where(labelId => !string.IsNullOrWhiteSpace(labelId))
                .Select(labelId => labelId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedLabelIds.Count == 0)
            {
                return Result<string>.Fail("条码列表不能为空。");
            }

            var result = await _inductionRackApi.LightUpLabelAsync(
                JsonSerializer.Serialize(normalizedLabelIds),
                (int)LightColorCode.Grey,
                1,
                string.Empty);

            if (IsRackApiFailure(result))
            {
                return Result<string>.Fail($"取消出库失败：{result}");
            }

            return Result<string>.Ok(result, "已取消出库。");
        }

        /// <summary>
        /// 处理感应料架拣货回调，区分正常出库和非法出库并更新库位状态。
        /// </summary>
        public async Task<Result<string>> HandlePickCallbackAsync(string labelId, string location, string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(labelId))
            {
                return Result<string>.Fail("条码不能为空。");
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                return Result<string>.Fail("库位码不能为空。");
            }

            var detailsResult = ParsePickDetails(detailsJson);
            if (!detailsResult.Success)
            {
                await NotifyCallbackAsync(labelId, location, detailsResult.Message!, false, false);
                return Result<string>.Fail(detailsResult.Message!);
            }

            var isIllegal = detailsResult.Data == null;
            if (!isIllegal)
            {
                var warehouseValidation = ValidateWarehouseContext(detailsResult.Data!.WarehouseLocation);
                if (warehouseValidation != null)
                {
                    await NotifyCallbackAsync(labelId, location, warehouseValidation, false, false);
                    return Result<string>.Fail(warehouseValidation);
                }
            }

            var barDetail = await _context.BarDetails
                .FirstOrDefaultAsync(x => x.BarNo == labelId && x.isDelete != "Y");
            if (barDetail == null)
            {
                var message = $"条码 {labelId} 不存在。";
                await NotifyCallbackAsync(labelId, location, message, false, isIllegal);
                return Result<string>.Fail(message);
            }

            var shelfDetail = await _context.ShelfDetails
                .FirstOrDefaultAsync(x => x.BinNo == location && x.DeleteFlag == "N");
            if (shelfDetail == null)
            {
                var message = $"库位 {location} 不存在。";
                await NotifyCallbackAsync(labelId, location, message, false, isIllegal);
                return Result<string>.Fail(message);
            }

            if (shelfDetail.IsInduction != "Y")
            {
                var message = $"库位 {location} 不是感应料架库位。";
                await NotifyCallbackAsync(labelId, location, message, false, isIllegal);
                return Result<string>.Fail(message);
            }

            if (!string.Equals(barDetail.BinNo, location, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"条码 {labelId} 当前库位与回调库位 {location} 不一致。";
                await NotifyCallbackAsync(labelId, location, message, false, isIllegal);
                return Result<string>.Fail(message);
            }

            if (!isIllegal)
            {
                var warehouseNo = detailsResult.Data!.WarehouseLocation;
                if (!string.Equals(shelfDetail.WarehouseNo, warehouseNo, StringComparison.OrdinalIgnoreCase))
                {
                    var message = $"库位 {location} 不属于仓库 {warehouseNo}。";
                    await NotifyCallbackAsync(labelId, location, message, false, false);
                    return Result<string>.Fail(message);
                }

                if (shelfDetail.IsEnable != "N")
                {
                    var message = $"库位 {location} 当前不是已占用状态，无法确认正常出库。";
                    await NotifyCallbackAsync(labelId, location, message, false, false);
                    return Result<string>.Fail(message);
                }
            }

            string messageText;
            var success = false;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                shelfDetail.IsEnable = "Y";

                if (!isIllegal)
                {
                    barDetail.IsRack = "N";
                    barDetail.BinNo = null;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                messageText = isIllegal
                    ? $"条码 {labelId} 非法出库。"
                    : $"条码 {labelId} 出库成功，库位：{location}";
                success = !isIllegal;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                messageText = isIllegal
                    ? $"非法出库处理失败：{ex.Message}"
                    : $"出库事务失败：{ex.Message}";
            }

            await NotifyCallbackAsync(labelId, location, messageText, success, isIllegal);
            return success ? Result<string>.Ok(messageText) : Result<string>.Fail(messageText);
        }

        /// <summary>
        /// 构建感应拣货条码查询，按入库时间和料架顺序提供稳定出库顺序。
        /// </summary>
        private IQueryable<InductionPickItem> BuildPickQuery(string itemNoKeyword, string warehouseLocation)
        {
            var likeKeyword = $"%{itemNoKeyword}%";

            return from bar in _context.BarDetails
                   join item in _context.itemDetails on bar.ItemGuid equals item.Guid
                   join shelf in _context.ShelfDetails on bar.BinNo equals shelf.BinNo
                   where EF.Functions.Like(item.ItemNo, likeKeyword)
                       && bar.IsRack == "Y"
                       && bar.isDelete != "Y"
                       && bar.EnableFlag == "Y"
                       && (bar.BarQty ?? 0m) > 0m
                       && shelf.IsInduction == "Y"
                       && shelf.DeleteFlag == "N"
                       && shelf.IsEnable == "N"
                       && shelf.WarehouseNo == warehouseLocation
                   orderby bar.InstockDate,
                           shelf.ShelfNo,
                           shelf.Row,
                           (shelf.SortDirection ?? "ASC") == "DESC" ? -shelf.Column : shelf.Column,
                           shelf.BinNo,
                           bar.BarNo
                   select new InductionPickItem
                   {
                       BarNo = bar.BarNo ?? string.Empty,
                       ItemNo = item.ItemNo ?? string.Empty,
                       BarQty = bar.BarQty ?? 0m,
                       BinNo = bar.BinNo ?? string.Empty,
                       InstockDate = bar.InstockDate ?? DateTime.MinValue,
                       Status = 0
                   };
        }

        /// <summary>
        /// 解析出库回调上下文，空上下文或非正常出库类型按非法出库处理。
        /// </summary>
        private Result<InductionPickDetails?> ParsePickDetails(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
            {
                return Result<InductionPickDetails?>.Ok(null, "空 DetailsJson 视为非法出库。");
            }

            try
            {
                var details = JsonSerializer.Deserialize<InductionPickDetails>(detailsJson);
                if (details == null || details.OutStockType != 2)
                {
                    return Result<InductionPickDetails?>.Ok(null, "OutStockType 非 2 视为非法出库。");
                }

                if (string.IsNullOrWhiteSpace(details.WarehouseLocation))
                {
                    return Result<InductionPickDetails?>.Fail("出库回调 DetailsJson 缺少仓库编码。");
                }

                return Result<InductionPickDetails?>.Ok(details, "解析成功。");
            }
            catch (JsonException)
            {
                return Result<InductionPickDetails?>.Fail("出库回调 DetailsJson 解析失败。");
            }
        }

        /// <summary>
        /// 校验仓库编码是否存在且属于感应料架允许范围。
        /// </summary>
        private string? ValidateWarehouseContext(string? warehouseLocation)
        {
            if (string.IsNullOrWhiteSpace(warehouseLocation))
            {
                return "仓库编码不能为空。";
            }

            if (_allowedWarehouses.Count == 0)
            {
                return "未配置感应料架允许仓库。";
            }

            if (!_allowedWarehouses.Contains(warehouseLocation))
            {
                return $"仓库 {warehouseLocation} 不在感应料架允许列表中。";
            }

            return null;
        }

        /// <summary>
        /// 将拣货回调处理结果广播给前端感应拣货页面。
        /// </summary>
        private async Task NotifyCallbackAsync(string labelId, string location, string message, bool success, bool isIllegal)
        {
            await _hubContext.NotifyPickCallbackAsync(new PickCallbackMessage
            {
                Success = success,
                LabelId = labelId,
                Location = location,
                Message = message,
                IsIllegal = isIllegal
            });
        }

        /// <summary>
        /// 判断外部料架接口返回文本是否表示失败或超时。
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
    }
}
