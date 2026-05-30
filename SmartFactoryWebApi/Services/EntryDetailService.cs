using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 处理普通入库流程的领域逻辑。
    /// </summary>
    public class EntryDetailService : IEntryDetailService
    {
        /// <summary>
        /// 访问 WMS 托盘、条码、库存、库位和仓库数据的数据库上下文。
        /// </summary>
        private readonly WmsDbContext _context;

        /// <summary>
        /// 初始化普通入库服务依赖的数据库上下文。
        /// </summary>
        public EntryDetailService(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 根据托盘码查询托盘与条码关系，用于兼容整托入库扫描。
        /// </summary>
        public async Task<Result<IEnumerable<PalletDetail>>> GetPalletDetailsAsync(string code)
        {
            var result = await _context.PalletDetails
                .Where(x => x.PalletNo == code)
                .ToListAsync();

            if (!result.Any())
            {
                return Result<IEnumerable<PalletDetail>>.Fail("未找到对应托盘信息");
            }
            return Result<IEnumerable<PalletDetail>>.Ok(result, "查询托盘信息成功");
        }

        /// <summary>
        /// 根据条码查询条码明细，用于兼容散件入库扫描。
        /// </summary>
        public async Task<Result<IEnumerable<BarDetail>>> GetBarDetailsAsync(string code)
        {
            var result = await _context.BarDetails
                .Where(x => x.BarNo == code)
                .ToListAsync();

            if (!result.Any())
            {
                return Result<IEnumerable<BarDetail>>.Fail("未找到对应条码信息");
            }
            return Result<IEnumerable<BarDetail>>.Ok(result, "查询条码信息成功");
        }

        /// <summary>
        /// 根据起始库位和扫码值预分配一组可入库库位，并返回条码到库位的映射。
        /// </summary>
        public async Task<Result<IEnumerable<PalletBarRelation>>> AllocateAsync(string barcode, string binNo)
        {
            if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(binNo))
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail("参数不完整");
            }

            var startBins = await _context.ShelfDetails
                .Where(x => x.BinNo == binNo && x.DeleteFlag == "N")
                .ToListAsync();

            if (startBins.Count == 0)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail("输入储位不存在");
            }

            if (startBins.Count > 1)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail($"储位 {binNo} 存在重复配置，请联系管理员检查主数据");
            }

            var startBin = startBins[0];
            if (startBin.IsEnable == "N")
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail("输入储位不可用");
            }

            if (startBin.IsInduction == "Y")
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail("当前库位属于感应式库位，不能走普通入库流程");
            }

            // 先把扫描值解析为实际参与分配的条码集合，既兼容托盘码，也兼容散件码。
            var palletDetails = (await GetPalletDetailsAsync(barcode)).Data;
            List<PalletBarRelation> items;
            if (palletDetails != null && palletDetails.Any())
            {
                items = palletDetails.Select(p => new PalletBarRelation
                {
                    PalletNo = p.PalletNo,
                    BarNo = p.BarNo
                }).ToList();
            }
            else
            {
                var barDetails = (await GetBarDetailsAsync(barcode)).Data;
                if (barDetails == null || !barDetails.Any())
                {
                    return Result<IEnumerable<PalletBarRelation>>.Fail($"未找到条码：{barcode}");
                }

                items = barDetails.Select(b => new PalletBarRelation
                {
                    BarNo = b.BarNo
                }).ToList();
            }

            // 按 BarNo 排序，确保条码按顺序分配库位（如 SN001→L001, SN002→L002）
            items = items.OrderBy(x => x.BarNo).ToList();

            // 检查条码是否已经入库（IsRack = 'Y'），防止重复分配导致原库位永久占用
            var barNosToCheck = items.Select(x => x.BarNo).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var alreadyRacked = await _context.BarDetails
                .Where(x => barNosToCheck.Contains(x.BarNo) && x.IsRack == "Y")
                .Select(x => x.BarNo)
                .ToListAsync();
            if (alreadyRacked.Count > 0)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail($"条码已入库，请勿重复操作：{string.Join(", ", alreadyRacked)}");
            }

            // 在真正入库前先预演库位分配，避免二次确认时才发现该行容量不足。
            var check = await IsColumnAvailableAsync(startBin, items.Count);
            if (!check.IsAvailable)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail(check.Error ?? "库位分配失败");
            }

            // 预演成功后，按条码顺序和库位顺序一一建立映射关系。
            for (var i = 0; i < items.Count; i++)
            {
                items[i] = new PalletBarRelation
                {
                    PalletNo = items[i].PalletNo,
                    BarNo = items[i].BarNo,
                    ShelfNo = startBin.ShelfNo,
                    BinNo = check.BinNos[i]
                };
            }

            return Result<IEnumerable<PalletBarRelation>>.Ok(items, "分配成功");
        }

        /// <summary>
        /// 在事务内提交普通入库结果，移动库存、更新条码库位并占用目标货架库位。
        /// </summary>
        public async Task<Result<IEnumerable<PalletBarRelation>>> CommitAsync(IEnumerable<PalletBarRelation> items, string warehouseLocation)
        {
            var relations = items?
                .Where(x => !string.IsNullOrWhiteSpace(x.BarNo) &&
                            !string.IsNullOrWhiteSpace(x.BinNo) &&
                            !string.IsNullOrWhiteSpace(x.ShelfNo))
                .ToList();

            // 入库事务必须限定在当前仓库范围内，避免跨仓库库位串用。
            var warehouseLocationDetail = await _context.WarehouseDetails
                .FirstOrDefaultAsync(x => x.WarehouseNo == warehouseLocation && x.DeleteFlag == "N");

            if (warehouseLocationDetail == null)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail($"仓库编码无效：{warehouseLocation}");
            }

            if (relations == null || relations.Count == 0)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail("无有效的入库数据");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var barNos = relations.Select(x => x.BarNo!).Distinct().ToList();
                var bars = await _context.BarDetails
                    .Where(x => barNos.Contains(x.BarNo))
                    .ToListAsync();

                if (bars.Count != barNos.Count)
                {
                    return Result<IEnumerable<PalletBarRelation>>.Fail("存在未找到的条码记录，请重新扫描分配库位");
                }

                // 后续事务需要同时读取目标库位和原库位的库存上下文。
                var targetBinNos = relations.Select(x => x.BinNo!).Distinct();
                var sourceBinNos = bars.Select(x => x.BinNo)
                                       .Where(x => !string.IsNullOrWhiteSpace(x));
                var allBinNos = targetBinNos.Union(sourceBinNos).ToList();

                // 预取库位、仓库和库存信息，减少事务内重复查询。
                var bins = await _context.BinDetails
                    .Where(x => allBinNos.Contains(x.BinNo) && x.FromGuid == warehouseLocationDetail.Guid)
                    .ToDictionaryAsync(x => x.BinNo!, x => x);

                var warehouseNos = bars
                    .Select(x => x.WarehouseNo)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                var warehouses = await _context.WarehouseDetails
                    .Where(x => warehouseNos.Contains(x.WarehouseNo))
                    .ToDictionaryAsync(x => x.WarehouseNo!, x => x);

                var stockBinGuids = bins.Values.Select(x => x.Guid).ToList();

                var stockCache = await _context.StockDetails
                    .Where(x => x.DeleteFlag == "N"
                        && stockBinGuids.Contains(x.BinGuid))
                    .ToListAsync();

                var targetBinGuidToNo = relations
                    .Select(x => x.BinNo!)
                    .Distinct()
                    .Where(bins.ContainsKey)
                    .ToDictionary(targetBinNo => bins[targetBinNo].Guid, targetBinNo => targetBinNo);

                var occupiedTargetBins = stockCache
                    .Where(x => x.StockQty > 0 && targetBinGuidToNo.ContainsKey(x.BinGuid))
                    .Select(x => targetBinGuidToNo[x.BinGuid])
                    .Distinct()
                    .ToList();
                if (occupiedTargetBins.Count > 0)
                {
                    throw new InvalidOperationException($"目标库位已有库存记录，请重新分配库位：{string.Join(", ", occupiedTargetBins)}");
                }

                var stockMap = stockCache.ToDictionary(
                    x => BuildStockKey(x.ItemGuid, x.WarehouseGuid, x.BinGuid, x.LotNo),
                    x => x);

                // 在事务内再次确认目标库位可用，避免分配阶段与提交阶段之间被其他终端抢占。
                var targetBinNoList = relations.Select(x => x.BinNo!).Distinct().ToList();
                var availableBins = await _context.ShelfDetails
                    .Where(x => targetBinNoList.Contains(x.BinNo)
                        && x.WarehouseNo == warehouseLocation
                        && x.IsEnable == "Y")
                    .Select(x => x.BinNo)
                    .Distinct()
                    .ToListAsync();
                if (availableBins.Count != targetBinNoList.Count)
                {
                    var occupied = targetBinNoList.Except(availableBins!);
                    throw new InvalidOperationException($"目标库位已被占用，请重新扫描分配库位：{string.Join(", ", occupied)}");
                }

                // 每个条码都要同时更新库存、条码状态和智能货架库位占用。
                foreach (var relation in relations)
                {
                    var bar = bars.First(x => x.BarNo == relation.BarNo);

                    if (!warehouses.TryGetValue(bar.WarehouseNo, out var warehouseDetail))
                    {
                        throw new InvalidOperationException($"未找到仓库：{bar.WarehouseNo}");
                    }
                    // 目标储位用于新增库存并更新条码当前位置。
                    if (!bins.TryGetValue(relation.BinNo!, out var targetBin))
                    {
                        throw new InvalidOperationException($"未找到储位：{relation.BinNo} 检查条码是否已入库：{warehouseLocation}");
                    }

                    // 原储位用于扣减旧库存，保证整条库存链路守恒。
                    if (string.IsNullOrWhiteSpace(bar.BinNo) || !bins.TryGetValue(bar.BinNo, out var sourceBin))
                    {
                        throw new InvalidOperationException($"未找到原储位：{bar.BinNo} 检查条码是否属于仓库编码:{warehouseLocation}");
                    }

                    var moveQty = bar.BarQty ?? 0m;
                    if (moveQty <= 0)
                    {
                        throw new InvalidOperationException($"条码 {bar.BarNo} 数量无效：{bar.BarQty}");
                    }

                    var now = DateTime.Now;

                    // 1) 扣减原库位库存并更新修改时间
                    var oldKey = BuildStockKey(bar.ItemGuid!, warehouseDetail.Guid, sourceBin.Guid, bar.LotNo);
                    if (!stockMap.TryGetValue(oldKey, out var oldStock))
                    {
                        throw new InvalidOperationException($"未找到原库位库存：{bar.BinNo}");
                    }
                    if ((oldStock.StockQty ?? 0m) < moveQty)
                    {
                        throw new InvalidOperationException($"库存不足：条码 {bar.BarNo} 当前库存 {oldStock.StockQty}，需扣减 {moveQty}");
                    }
                    oldStock.StockQty = (oldStock.StockQty ?? 0m) - moveQty;
                    oldStock.ModifyTime = now;

                    // 2) 目标库位如存在残留的 0 数量库存记录，则直接复用该行，避免撞库存唯一键。
                    var targetKey = BuildStockKey(bar.ItemGuid!, warehouseDetail.Guid, targetBin.Guid, bar.LotNo);
                    if (stockMap.TryGetValue(targetKey, out var targetStock))
                    {
                        targetStock.StockQty = (targetStock.StockQty ?? 0m) + moveQty;
                        targetStock.ModifyTime = now;
                    }
                    else
                    {
                        var newStock = new StockDetail
                        {
                            Guid = Guid.NewGuid().ToString(),
                            ItemGuid = bar.ItemGuid!,
                            WarehouseGuid = warehouseDetail.Guid,
                            BinGuid = targetBin.Guid,
                            LotNo = bar.LotNo ?? string.Empty,
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
                            ModifyTime = now,
                            Flag = oldStock.Flag,
                            DeleteFlag = oldStock.DeleteFlag
                        };
                        await _context.StockDetails.AddAsync(newStock);
                        stockMap[targetKey] = newStock;
                    }

                    // 3) 更新条码库位与入库智能货架标记
                    bar.BinNo = relation.BinNo;
                    bar.IsRack = "Y";
                    bar.InstockDate = now;

                    // 4) 根据目标 BinNo 更新智能货架库位表的 IS_ENABLE = 'N'
                    var shelf = await _context.ShelfDetails
                        .FirstOrDefaultAsync(s => s.BinNo == relation.BinNo && s.WarehouseNo == warehouseLocation);

                    if (shelf != null)
                    {
                        shelf.IsEnable = "N";
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result<IEnumerable<PalletBarRelation>>.Ok(relations, "入库成功");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result<IEnumerable<PalletBarRelation>>.Fail(GetCommitErrorMessage(ex));
            }
        }

        /// <summary>
        /// 根据起始库位和数量要求，预演同一行可分配的逻辑库位列表。
        /// </summary>
        private async Task<(bool IsAvailable, List<string> BinNos, string? Error)> IsColumnAvailableAsync(ShelfDetail startBin, int requiredQty)
        {
            if (requiredQty <= 0)
            {
                return (false, new List<string>(), "无有效条码数量");
            }

            var sortDirection = startBin.SortDirection ?? "ASC";
            var binSize = startBin.BinSize > 0 ? startBin.BinSize : 1;
            var startColumn = startBin.Column;
            var row = startBin.Row;
            var shelfNo = startBin.ShelfNo;

            List<string> allocated;

            if (sortDirection == "DESC")
            {
                // 降序：从起始列往低列分配
                var binDetailList = await _context.ShelfDetails
                    .Where(x => x.ShelfNo == shelfNo
                        && x.Row == row
                        && x.Column <= startColumn
                        && x.IsEnable == "Y"
                        && x.DeleteFlag == "N"
                        && x.IsInduction == "N")
                    .OrderByDescending(x => x.Column)
                    .ToListAsync();

                allocated = binDetailList
                    .Where(x => (startColumn - x.Column) % binSize == 0)
                    .Where(x => !string.IsNullOrWhiteSpace(x.BinNo))
                    .Select(x => x.BinNo)
                    .Distinct()
                    .Take(requiredQty)
                    .ToList();
            }
            else
            {
                // 升序：从起始列往高列分配
                var binDetailList = await _context.ShelfDetails
                    .Where(x => x.ShelfNo == shelfNo
                        && x.Row == row
                        && x.Column >= startColumn
                        && x.IsEnable == "Y"
                        && x.DeleteFlag == "N"
                        && x.IsInduction == "N")
                    .OrderBy(x => x.Column)
                    .ToListAsync();

                allocated = binDetailList
                    .Where(x => (x.Column - startColumn) % binSize == 0)
                    .Where(x => !string.IsNullOrWhiteSpace(x.BinNo))
                    .Select(x => x.BinNo)
                    .Distinct()
                    .Take(requiredQty)
                    .ToList();
            }

            if (allocated.Count < requiredQty)
            {
                return (false, allocated, "该行剩余库位不足");
            }

            return (true, allocated, null);
        }

        /// <summary>
        /// 构建库存缓存键，用于事务内定位同物料、同仓库、同库位和同批次库存记录。
        /// </summary>
        private static string BuildStockKey(string itemGuid, string warehouseGuid, string binGuid, string? lotNo)
        {
            return $"{itemGuid}-{warehouseGuid}-{binGuid}-{lotNo ?? string.Empty}";
        }

        /// <summary>
        /// 从提交异常链中提取可读错误信息，并对库存唯一键冲突给出业务化提示。
        /// </summary>
        private static string GetCommitErrorMessage(Exception ex)
        {
            var messages = new List<string>();

            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message?.Trim();
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                var isGenericEfSaveMessage = current is DbUpdateException
                    && current.InnerException != null
                    && message.Contains("An error occurred while saving the entity changes", StringComparison.OrdinalIgnoreCase);

                if (isGenericEfSaveMessage)
                {
                    continue;
                }

                if (messages.Any(existing => string.Equals(existing, message, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                messages.Add(message);
            }

            if (messages.Count == 0)
            {
                return "保存入库数据时发生未知错误";
            }

            var detail = string.Join(" -> ", messages);
            if (detail.Contains("UK_WMS_ITEM_STOCK_KEY", StringComparison.OrdinalIgnoreCase))
            {
                return "目标库位库存记录冲突，请检查该库位是否存在残留库存或重复数据";
            }

            return detail;
        }
    }
}
