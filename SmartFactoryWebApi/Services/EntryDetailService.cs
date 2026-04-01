using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    public class EntryDetailService : IEntryDetailService
    {
        private readonly WmsDbContext _context;

        public EntryDetailService(WmsDbContext context)
        {
            _context = context;
        }

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

        // 分配条码库位
        public async Task<Result<IEnumerable<PalletBarRelation>>> AllocateAsync(string barcode, string shelf, string binNo)
        {
            if (string.IsNullOrWhiteSpace(barcode) || string.IsNullOrWhiteSpace(shelf) || string.IsNullOrWhiteSpace(binNo))
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail("参数不完整");
            }

            // 1) 解析条码（托盘或散件）
            var palletDetails = (await GetPalletDetailsAsync(barcode)).Data;
            List<PalletBarRelation> items;
            if (palletDetails != null &&  palletDetails.Any())
            {
                items = palletDetails.Select(p => new PalletBarRelation
                {
                    PalletNo = p.PalletNo,
                    BarNo = p.BarNo
                }).ToList();
            }
            // 2) 不是托盘码，尝试按散件条码处理
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

            // 2) 预取分配库位
            var check = await IsColumnAvailableAsync(shelf, binNo, items.Count);
            if (!check.IsAvailable)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail(check.Error);
            }

            // 条码递增分配库位
            for (var i = 0; i < items.Count; i++)
            {
                items[i] = new PalletBarRelation
                {
                    PalletNo = items[i].PalletNo,
                    BarNo = items[i].BarNo,
                    ShelfNo = shelf,
                    BinNo = check.BinNos[i]
                };
            }

            return Result<IEnumerable<PalletBarRelation>>.Ok(items, "分配成功");
        }

        /// <summary>
        /// 条码入库事务：更新条码库位/上架标记，同时维护 WMS_ITEM_STOCK。
        /// </summary>
        public async Task<Result<IEnumerable<PalletBarRelation>>> CommitAsync(IEnumerable<PalletBarRelation> items, string warehouseLocation)
        {
            var relations = items?
                .Where(x => !string.IsNullOrWhiteSpace(x.BarNo) &&
                            !string.IsNullOrWhiteSpace(x.BinNo) &&
                            !string.IsNullOrWhiteSpace(x.ShelfNo))
                .ToList();

            // 将库位取值限定在某个仓库范围内
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

                // 需要的库位：目标库位 + 原库位
                var targetBinNos = relations.Select(x => x.BinNo!).Distinct();
                var sourceBinNos = bars.Select(x => x.BinNo)
                                       .Where(x => !string.IsNullOrWhiteSpace(x));
                var allBinNos = targetBinNos.Union(sourceBinNos).ToList();
                

                // 查询WMS的库位表数据
                var bins = await _context.BinDetails
                    .Where(x => allBinNos.Contains(x.BinNo) && x.FromGuid == warehouseLocationDetail.Guid)
                    .ToDictionaryAsync(x => x.BinNo!, x => x);

                // 获取原条码所有相关原仓库信息
                var warehouseNos = bars
                    .Select(x => x.WarehouseNo)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
                
                var warehouses = await _context.WarehouseDetails
                    .Where(x => warehouseNos.Contains(x.WarehouseNo))
                    .ToDictionaryAsync(x => x.WarehouseNo!, x => x);

                // 获取原条码所有相关物料信息
                var itemGuids = bars
                    .Select(x => x.ItemGuid)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                // 预取相关库存
                var stockBinGuids = bins.Values.Select(x => x.Guid).ToList();
                var stockWarehouseGuids = warehouses.Values.Select(x => x.Guid).ToList();

                var stockCache = await _context.StockDetails
                    .Where(x => itemGuids.Contains(x.ItemGuid)
                        && stockBinGuids.Contains(x.BinGuid)
                        && stockWarehouseGuids.Contains(x.WarehouseGuid))
                    .ToListAsync();

                var stockMap = stockCache.ToDictionary(
                    x => $"{x.ItemGuid}-{x.WarehouseGuid}-{x.BinGuid}-{x.LotNo}",
                    x => x);

                // 验证目标库位在事务内仍为可用（防止并发 TOCTOU）
                var targetBinNoList = relations.Select(x => x.BinNo!).Distinct().ToList();
                var availableBins = await _context.ShelfDetails
                    .Where(x => targetBinNoList.Contains(x.BinNo) && x.IsEnable == "Y")
                    .Select(x => x.BinNo)
                    .ToListAsync();
                if (availableBins.Count != targetBinNoList.Count)
                {
                    var occupied = targetBinNoList.Except(availableBins!);
                    throw new InvalidOperationException($"目标库位已被占用，请重新扫描分配库位：{string.Join(", ", occupied)}");
                }

                // 开始将每一个条码进行入库
                foreach (var relation in relations)
                {
                    var bar = bars.First(x => x.BarNo == relation.BarNo);

                    if (!warehouses.TryGetValue(bar.WarehouseNo, out var warehouseDetail))
                    {
                        throw new InvalidOperationException($"未找到仓库：{bar.WarehouseNo}");
                    }
                    // 入库的储位
                    if (!bins.TryGetValue(relation.BinNo!, out var targetBin))
                    {
                        throw new InvalidOperationException($"未找到储位：{relation.BinNo} 检查条码是否已入库：{warehouseLocation}");
                    }
                    // 原储位
                    if (string.IsNullOrWhiteSpace(bar.BinNo) || !bins.TryGetValue(bar.BinNo, out var sourceBin))
                    {
                        throw new InvalidOperationException($"未找到原储位：{bar.BinNo} 检查条码是否属于仓库编码:{warehouseLocation}");
                    }

                    // 1) 扣减原库位库存并更新修改时间
                    var oldKey = $"{bar.ItemGuid}-{warehouseDetail.Guid}-{sourceBin.Guid}-{bar.LotNo}";
                    if (!stockMap.TryGetValue(oldKey, out var oldStock))
                    {
                        throw new InvalidOperationException($"未找到原库位库存：{bar.BinNo}");
                    }
                    if (oldStock.StockQty < bar.BarQty)
                    {
                        throw new InvalidOperationException($"库存不足：条码 {bar.BarNo} 当前库存 {oldStock.StockQty}，需扣减 {bar.BarQty}");
                    }
                    oldStock.StockQty -= bar.BarQty;
                    oldStock.ModifyTime = DateTime.Now;

                    // 2) 为目标库位新增库存记录（每条码一条）
                    var newStock = new StockDetail
                    {
                        Guid = Guid.NewGuid().ToString(),
                        ItemGuid = bar.ItemGuid!,
                        WarehouseGuid = warehouseDetail.Guid,
                        BinGuid = targetBin.Guid,
                        LotNo = bar.LotNo ?? string.Empty,
                        StockQty = bar.BarQty,
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
                    };
                    await _context.StockDetails.AddAsync(newStock);

                    // 3) 更新条码库位与入库智能货架标记
                    bar.BinNo = relation.BinNo;
                    bar.IsRack = "Y";

                    // 4) 根据目标 BinNo 更新 智能货架库位表的IS_ENABLE = 'N'，标识该库位已被占用，后续不能再入库该库位
                    var shelf = await _context.ShelfDetails
                        .FirstOrDefaultAsync(s => s.BinNo == relation.BinNo);

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
                return Result<IEnumerable<PalletBarRelation>>.Fail($"入库失败：{ex.Message}");
            }
        }

        // 查询库位该行 x 个条码可用库位
        // 参数说明：
        // shelf：货架号
        // binNo：起始库位号（物理标签）
        // requiredQty: 需要放置条码数量，一个条码分配一个库位
        private async Task<(bool IsAvailable, List<string> BinNos, string? Error)> IsColumnAvailableAsync(string shelf, string binNo, int requiredQty)
        {
            if (requiredQty <= 0)
            {
                return (false, new List<string>(), "无有效条码数量");
            }

            // 1. 查找逻辑库位（处理大盘货架物理标签 → 逻辑库位的映射）
            var startBin = await FindLogicalBinAsync(shelf, binNo);
            if (startBin == null)
            {
                return (false, new List<string>(), "输入储位不存在或不可用");
            }

            // 2. 获取货架的 BinSize（大盘为3，普通为1）和分配方向
            string sortDirection = startBin.SortDirection ?? "ASC";
            int binSize = startBin.BinSize > 0 ? startBin.BinSize : 1;  // 递增步长
            int startColumn = startBin.Column;  // 起始分配列
            int row = startBin.Row; // 分配货架行

            List<string> allocated;

            if (sortDirection == "DESC")
            {
                // 降序：从起始列往低列分配
                var binDetailList = await _context.ShelfDetails
                    .Where(x => x.ShelfNo == shelf
                        && x.Row == row
                        && x.Column <= startColumn         // 起始列及之前
                        && x.IsEnable == "Y"
                        && x.DeleteFlag == "N"
                        && x.IsInduction == "N")
                    .OrderByDescending(x => x.Column)      // 从高到低
                    .ToListAsync();

                // 分配逻辑：按 BinSize 步长筛选（反向）
                allocated = binDetailList
                    .Where(x => (startColumn - x.Column) % binSize == 0)  // 按 binSize 步长取（反向）
                    .Where(x => !string.IsNullOrWhiteSpace(x.BinNo))
                    .Select(x => x.BinNo)
                    .Distinct()
                    .Take(requiredQty)
                    .ToList();
            }
            else
            {
                // 升序（原有逻辑）：从起始列往高列分配
                var binDetailList = await _context.ShelfDetails
                    .Where(x => x.ShelfNo == shelf
                        && x.Row == row
                        && x.Column >= startColumn
                        && x.IsEnable == "Y"
                        && x.DeleteFlag == "N"
                        && x.IsInduction == "N")
                    .OrderBy(x => x.Column)
                    .ToListAsync();

                // 分配逻辑：按 BinSize 步长筛选
                allocated = binDetailList
                    .Where(x => (x.Column - startColumn) % binSize == 0)  // 按 binSize 步长取
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
        /// 根据用户输入的物理库位号，查找数据库中的逻辑库位
        /// 大盘货架：用户输入 L110A1002 → 返回 L110A1001
        /// 普通货架：用户输入 L005A1001 → 返回 L005A1001
        /// </summary>
        private async Task<ShelfDetail?> FindLogicalBinAsync(string shelf, string physicalBinNo)
        {
            // 查找 <= physicalBinNo 的最近一条记录
            var logicalBin = await _context.ShelfDetails
                .Where(x => x.ShelfNo == shelf
                    && string.Compare(x.BinNo, physicalBinNo) <= 0 
                    && x.IsEnable == "Y"
                    && x.DeleteFlag == "N")
                .OrderByDescending(x => x.BinNo)
                .FirstOrDefaultAsync();

            return logicalBin;
        }
    }
}
