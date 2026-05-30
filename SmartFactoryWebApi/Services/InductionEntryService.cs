using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;
using System.Text.Json;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 处理感应入库的料架验证、入库请求、回调事务和前端通知。
    /// </summary>
    public class InductionEntryService : IInductionEntryService
    {
        /// <summary>
        /// 访问 WMS 库存、条码、库位和料架主数据的数据库上下文。
        /// </summary>
        private readonly WmsDbContext _context;

        /// <summary>
        /// 调用外部感应料架设备接口的服务。
        /// </summary>
        private readonly IInductionRackApiService _inductionRackApi;

        /// <summary>
        /// 向前端广播感应入库回调结果的 Hub 封装。
        /// </summary>
        private readonly IInductionHubContext _hubContext;

        /// <summary>
        /// 允许执行感应入库的仓库编码集合。
        /// </summary>
        private readonly HashSet<string> _allowedWarehouses;

        /// <summary>
        /// 初始化感应入库服务依赖，并读取允许仓库配置。
        /// </summary>
        public InductionEntryService(
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
        /// 校验料架是否存在、属于当前仓库且存在可用空库位。
        /// </summary>
        public async Task<Result<InductionShelfValidation>> ValidateShelfAsync(string shelfCode, string warehouseLocation)
        {
            if (string.IsNullOrWhiteSpace(shelfCode))
            {
                return Result<InductionShelfValidation>.Fail("料架号不能为空。");
            }

            var warehouseValidation = ValidateWarehouseContext(warehouseLocation);
            if (warehouseValidation != null)
            {
                return Result<InductionShelfValidation>.Fail(warehouseValidation);
            }

            var shelves = await _context.ShelfDetails
                .Where(x => x.ShelfNo == shelfCode && x.DeleteFlag == "N")
                .ToListAsync();

            if (shelves.Count == 0)
            {
                return Result<InductionShelfValidation>.Fail($"料架 {shelfCode} 不存在。");
            }

            if (shelves.All(x => x.IsInduction != "Y"))
            {
                return Result<InductionShelfValidation>.Fail($"料架 {shelfCode} 不是感应料架。");
            }

            if (shelves.All(x => !string.Equals(x.WarehouseNo, warehouseLocation, StringComparison.OrdinalIgnoreCase)))
            {
                return Result<InductionShelfValidation>.Fail($"料架 {shelfCode} 不属于仓库 {warehouseLocation}。");
            }

            var emptyLocationCount = shelves.Count(x =>
                x.IsInduction == "Y"
                && string.Equals(x.WarehouseNo, warehouseLocation, StringComparison.OrdinalIgnoreCase)
                && x.IsEnable == "Y");

            if (emptyLocationCount <= 0)
            {
                return Result<InductionShelfValidation>.Fail($"料架 {shelfCode} 没有可用空库位。");
            }

            return Result<InductionShelfValidation>.Ok(new InductionShelfValidation
            {
                IsValid = true,
                ShelfCode = shelfCode,
                WarehouseNo = warehouseLocation,
                EmptyLocationCount = emptyLocationCount
            }, "料架验证成功。");
        }

        /// <summary>
        /// 校验条码和料架后向外部感应料架发送入库指令。
        /// </summary>
        public async Task<Result<string>> DepositAsync(string barcode, string shelfCode, string warehouseLocation)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Result<string>.Fail("条码不能为空。");
            }

            var warehouseValidation = ValidateWarehouseContext(warehouseLocation);
            if (warehouseValidation != null)
            {
                return Result<string>.Fail(warehouseValidation);
            }

            var shelfValidation = await ValidateShelfAsync(shelfCode, warehouseLocation);
            if (!shelfValidation.Success)
            {
                return Result<string>.Fail(shelfValidation.Message ?? "料架验证失败。");
            }

            var barDetail = await _context.BarDetails
                .FirstOrDefaultAsync(x => x.BarNo == barcode && x.isDelete != "Y");

            if (barDetail == null)
            {
                return Result<string>.Fail($"条码 {barcode} 不存在。");
            }

            if (barDetail.IsRack == "Y")
            {
                return Result<string>.Fail($"条码 {barcode} 已入库，请勿重复操作。");
            }

            if (string.IsNullOrWhiteSpace(barDetail.WarehouseNo)
                || !string.Equals(barDetail.WarehouseNo, warehouseLocation, StringComparison.OrdinalIgnoreCase))
            {
                return Result<string>.Fail($"条码 {barcode} 不属于当前仓库 {warehouseLocation}。");
            }

            if (string.IsNullOrWhiteSpace(barDetail.BinNo))
            {
                return Result<string>.Fail($"条码 {barcode} 缺少原库位，无法执行感应入库。");
            }

            var operationTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            var detailsJson = JsonSerializer.Serialize(new InductionDepositDetails
            {
                WarehouseLocation = warehouseLocation,
                BarGuid = barDetail.Guid,
                SourceBinNo = barDetail.BinNo,
                OperationTime = operationTime
            });

            var result = await _inductionRackApi.DepositLabelAsync(barcode, shelfCode, operationTime, detailsJson);
            if (IsRackApiFailure(result))
            {
                return Result<string>.Fail($"调用感应料架入库接口失败：{result}");
            }

            return Result<string>.Ok(result, "已发送入库请求，等待料架回调。");
        }

        /// <summary>
        /// 取消指定条码在外部感应料架上的待入库状态。
        /// </summary>
        public async Task<Result<string>> CancelDepositAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return Result<string>.Fail("条码不能为空。");
            }

            var result = await _inductionRackApi.RemoveLabelAsync(barcode);
            if (IsRackApiFailure(result))
            {
                return Result<string>.Fail($"取消入库失败：{result}");
            }

            return Result<string>.Ok(result, "已取消入库。");
        }

        /// <summary>
        /// 处理感应料架入库回调，并在事务内移动库存、更新条码和占用目标库位。
        /// </summary>
        public async Task<Result<string>> HandleDepositCallbackAsync(string labelId, string location, string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(labelId))
            {
                return Result<string>.Fail("条码不能为空。");
            }

            if (string.IsNullOrWhiteSpace(location))
            {
                return Result<string>.Fail("库位码不能为空。");
            }

            var (details, error) = ParseDepositDetails(detailsJson);
            if (details == null)
            {
                await NotifyCallbackAsync(labelId, location, error, false);
                return Result<string>.Fail(error);
            }

            var warehouseValidation = ValidateWarehouseContext(details.WarehouseLocation);
            if (warehouseValidation != null)
            {
                await NotifyCallbackAsync(labelId, location, warehouseValidation, false);
                return Result<string>.Fail(warehouseValidation);
            }

            var barDetail = await _context.BarDetails
                .FirstOrDefaultAsync(x => x.BarNo == labelId && x.isDelete != "Y");
            if (barDetail == null)
            {
                var message = $"条码 {labelId} 不存在。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (!string.IsNullOrWhiteSpace(details.BarGuid)
                && !string.Equals(barDetail.Guid, details.BarGuid, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"条码 {labelId} 与回调上下文不匹配。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (string.IsNullOrWhiteSpace(barDetail.ItemGuid))
            {
                var message = $"条码 {labelId} 缺少物料信息。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (string.IsNullOrWhiteSpace(barDetail.WarehouseNo)
                || !string.Equals(barDetail.WarehouseNo, details.WarehouseLocation, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"条码 {labelId} 不属于仓库 {details.WarehouseLocation}。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            var sourceBinNo = !string.IsNullOrWhiteSpace(details.SourceBinNo) ? details.SourceBinNo : barDetail.BinNo;
            if (string.IsNullOrWhiteSpace(sourceBinNo))
            {
                var message = $"条码 {labelId} 缺少原库位，无法完成入库事务。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (string.Equals(sourceBinNo, location, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"条码 {labelId} 原库位与目标库位相同，无法重复上架。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            var targetShelf = await _context.ShelfDetails
                .FirstOrDefaultAsync(x => x.BinNo == location && x.DeleteFlag == "N");
            if (targetShelf == null)
            {
                var message = $"库位 {location} 不存在。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (targetShelf.IsInduction != "Y")
            {
                var message = $"库位 {location} 不是感应料架库位。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (!string.Equals(targetShelf.WarehouseNo, details.WarehouseLocation, StringComparison.OrdinalIgnoreCase))
            {
                var message = $"库位 {location} 不属于仓库 {details.WarehouseLocation}。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            if (targetShelf.IsEnable != "Y")
            {
                var message = $"库位 {location} 已被占用。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            var warehouseDetail = await _context.WarehouseDetails
                .FirstOrDefaultAsync(x => x.WarehouseNo == details.WarehouseLocation && x.DeleteFlag == "N");
            if (warehouseDetail == null)
            {
                var message = $"仓库 {details.WarehouseLocation} 不存在。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            var sourceBin = await _context.BinDetails
                .FirstOrDefaultAsync(x => x.BinNo == sourceBinNo && x.FromGuid == warehouseDetail.Guid);
            if (sourceBin == null)
            {
                var message = $"原库位 {sourceBinNo} 不属于仓库 {details.WarehouseLocation}。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            var targetBin = await _context.BinDetails
                .FirstOrDefaultAsync(x => x.BinNo == location && x.FromGuid == warehouseDetail.Guid);
            if (targetBin == null)
            {
                var message = $"目标库位 {location} 不属于仓库 {details.WarehouseLocation}。";
                await NotifyCallbackAsync(labelId, location, message, false);
                return Result<string>.Fail(message);
            }

            string messageText;
            var success = false;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var moveQty = barDetail.BarQty ?? 0m;
                if (moveQty <= 0)
                {
                    throw new InvalidOperationException($"条码 {labelId} 数量无效：{barDetail.BarQty}");
                }

                var lotNo = barDetail.LotNo ?? string.Empty;
                var oldStock = await _context.StockDetails
                    .FirstOrDefaultAsync(x =>
                        x.ItemGuid == barDetail.ItemGuid
                        && x.WarehouseGuid == warehouseDetail.Guid
                        && x.BinGuid == sourceBin.Guid
                        && x.LotNo == lotNo);
                if (oldStock == null)
                {
                    throw new InvalidOperationException($"条码 {labelId} 原库位库存不存在：{sourceBinNo}");
                }

                if ((oldStock.StockQty ?? 0m) < moveQty)
                {
                    throw new InvalidOperationException($"条码 {labelId} 原库位库存不足，当前库存 {(oldStock.StockQty ?? 0m)}。");
                }

                oldStock.StockQty = (oldStock.StockQty ?? 0m) - moveQty;
                oldStock.ModifyTime = DateTime.Now;

                var targetStock = await _context.StockDetails
                    .FirstOrDefaultAsync(x =>
                        x.ItemGuid == barDetail.ItemGuid
                        && x.WarehouseGuid == warehouseDetail.Guid
                        && x.BinGuid == targetBin.Guid
                        && x.LotNo == lotNo);

                if (targetStock != null)
                {
                    if ((targetStock.StockQty ?? 0m) > 0m)
                    {
                        throw new InvalidOperationException($"目标库位 {location} 已存在库存记录。");
                    }

                    targetStock.StockQty = moveQty;
                    targetStock.ModifyTime = DateTime.Now;
                }
                else
                {
                    await _context.StockDetails.AddAsync(new StockDetail
                    {
                        Guid = Guid.NewGuid().ToString(),
                        ItemGuid = barDetail.ItemGuid,
                        WarehouseGuid = warehouseDetail.Guid,
                        BinGuid = targetBin.Guid,
                        LotNo = lotNo,
                        StockQty = moveQty,
                        LastReceiptDate = oldStock.LastReceiptDate,
                        LastQcDate = oldStock.LastQcDate,
                        PalletNo = oldStock.PalletNo,
                        OrderNo = oldStock.OrderNo,
                        OrderSeq = oldStock.OrderSeq,
                        Creator = oldStock.Creator,
                        CreateTime = oldStock.CreateTime,
                        Factory = oldStock.Factory,
                        Modifier = oldStock.Modifier,
                        ModifyTime = DateTime.Now,
                        Flag = oldStock.Flag,
                        DeleteFlag = oldStock.DeleteFlag
                    });
                }

                barDetail.BinNo = location;
                barDetail.WarehouseNo = details.WarehouseLocation;
                barDetail.IsRack = "Y";
                barDetail.InstockDate = DateTime.Now;
                targetShelf.IsEnable = "N";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                messageText = $"条码 {labelId} 入库成功，库位：{location}";
                success = true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                messageText = $"入库事务失败：{ex.Message}";
            }

            await NotifyCallbackAsync(labelId, location, messageText, success);
            return success ? Result<string>.Ok(messageText) : Result<string>.Fail(messageText);
        }

        /// <summary>
        /// 解析入库指令携带的回调上下文，用于校验仓库和原库位。
        /// </summary>
        private (InductionDepositDetails? Details, string Error) ParseDepositDetails(string? detailsJson)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
            {
                return (null, "入库回调缺少 DetailsJson。");
            }

            try
            {
                var details = JsonSerializer.Deserialize<InductionDepositDetails>(detailsJson);
                if (details == null || string.IsNullOrWhiteSpace(details.WarehouseLocation))
                {
                    return (null, "入库回调 DetailsJson 无效。");
                }

                return (details, string.Empty);
            }
            catch (JsonException)
            {
                return (null, "入库回调 DetailsJson 解析失败。");
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
        /// 将入库回调处理结果广播给前端感应入库页面。
        /// </summary>
        private async Task NotifyCallbackAsync(string labelId, string location, string message, bool success)
        {
            await _hubContext.NotifyDepositCallbackAsync(new DepositCallbackMessage
            {
                Success = success,
                LabelId = labelId,
                Location = location,
                Message = message
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
