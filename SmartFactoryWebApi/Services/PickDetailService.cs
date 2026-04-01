using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;
using System.Data;
using System.IO.Pipelines;

namespace SmartFactoryWebApi.Services
{
    public class PickDetailService : IPickDetailService
    {

        // 1. 声明私有只读字段
        private readonly WmsDbContext _context;
        private readonly List<string> AllowWarehouseList = new() { "601", "617", "621" };

        // 2. 添加构造函数，参数就是你要注入的 DbContext
        // 容器会自动把创建好的 DbContext 传进来
        public PickDetailService(WmsDbContext context)
        {
            _context = context;
        }

        // 根据单号查明细
        public async Task<Result<List<PickDetail>>> GetPickDetailAsync(string docNo)
        {
            // 查询doGuid
            var resultGuid = await GetFromGuid(docNo);
            if (!resultGuid.Success)
            {
                return Result<List<PickDetail>>.Fail("未找到对应的领料单");
            }
            // LINQ 查询：会被翻译成 SELECT * FROM Stocks WHERE Barcode = ...
            var result = await _context.PickDetails
                           .Where(x => x.FromGuid == resultGuid.Data)
                           .ToListAsync();
            return Result<List<PickDetail>>.Ok(result, "查询领料单明细成功");
        }

        public async Task<Result<bool>> isExistPickApplyDoc(string docno)
        {
            var exists = await _context.PickApplies
                           .AnyAsync(x => x.DocNo == docno);
            if (!exists)
            {
                return Result<bool>.Fail("未找到对应的领料单");
            }
            return Result<bool>.Ok(exists, "查询领料单是否存在成功");
        }

        public async Task<Result<string>> GetFromGuid(string docno)
        {
            var pickApply = await _context.PickApplies
                                   .FirstOrDefaultAsync(x => x.DocNo == docno);
            if (pickApply != null && !string.IsNullOrEmpty(pickApply.Guid))
            {
                return Result<string>.Ok(pickApply.Guid, "查询GUID成功");
            }
            return Result<string>.Fail("未找到对应的领料单");
        }

        public async Task<Result<string>> GetItemNoByItemGuid(string itemGuid)
        {
            var item = await _context.itemDetails
                           .FirstOrDefaultAsync(x => x.Guid == itemGuid);
            if (item != null && !string.IsNullOrEmpty(item.ItemNo))
            {
                return Result<string>.Ok(item.ItemNo, "查询物料编号成功");
            }
            return Result<string>.Fail("未找到对应的物料");
        }

        // 根据需求数量、料号、仓库 FIFO+分页查询条码
        public async Task<Result<List<BarDetail>>> GetBarByRequiredQtyQtyAndProductNo(decimal? needQty, string itemGuid, string warehouseLocation)
        {
            if (needQty <= 0 || string.IsNullOrWhiteSpace(itemGuid))
            {
                return Result<List<BarDetail>>.Fail("无效的查询参数");
            }

            // 检查warehouseLocation是否在允许列表中
            if (!AllowWarehouseList.Contains(warehouseLocation))
            {
                return Result<List<BarDetail>>.Fail($"仓库编码无效：{warehouseLocation}");
            }


            const int pageSize = 200; // 分页查询：可按实际调优
            var selectedBars = new List<BarDetail>();
            var remaining = needQty;
            var pageIndex = 0;

            // 获取当前仓库的锁定条码（按仓库过滤，避免跨仓库误排除）
            var lockedBarNos = _context.LockedBarNos
                .Where(x => x.WarehouseLocation == warehouseLocation)
                .Select(x => x.BarNo)
                .ToHashSet();

            while (remaining > 0)
            {

                // 根据itemGuid查出所有条码，并满足needqty选出最早的条码列表
                var query = _context.BarDetails
                                // 放置条码查询条件
                                .Where(x =>
                                    x.ItemGuid == itemGuid &&   // 对应料号的条码
                                    x.IsRack == "Y" &&          // 存在于智能货架的条码
                                    x.WarehouseNo == warehouseLocation && // 根据仓库位置过滤条码
                                    x.EnableFlag == "Y" &&      // 是否有效
                                    x.BarQty > 0 &&             // 条码数量大于0
                                    x.isDelete == "N");         // 没有被逻辑删除
                
                // 排除锁定条码
                if(lockedBarNos.Count > 0)
                {
                    query = query.Where(x => !lockedBarNos.Contains(x.BarNo));
                }

                // 排序+分页查询
                var orderedBars = await query
                                        .OrderBy(x => x.InstockDate)
                                        .Skip(pageIndex * pageSize)
                                        .Take(pageSize)
                                        .ToListAsync();

                // 如果没有更多条码可用，跳出循环
                if (!orderedBars.Any())
                {
                    break;
                }

                // 有则继续扣减
                foreach (var bar in orderedBars)
                {
                    selectedBars.Add(bar);
                    // 减少剩余需求量
                    remaining -= bar.BarQty ?? 0;
                    if (remaining <= 0) break;
                }

                pageIndex++; // 增加页码以继续查询下一页
            }

            return Result<List<BarDetail>>.Ok(selectedBars, "查询条码成功");
        }

        // 根据领料单分配条码
        public async Task<Result<List<VariableItem>>> ReserveBarsByDocNoAsync(string docNo, string warehouseLocation)
        {
            if (string.IsNullOrWhiteSpace(docNo))
            {
                return Result<List<VariableItem>>.Fail("领料单号不能为空");
            }

            if (!AllowWarehouseList.Contains(warehouseLocation))
            {
                return Result<List<VariableItem>>.Fail($"仓库编码无效：{warehouseLocation}");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // 查询是否存在已锁定条码（按仓库过滤）
                var existingLocksResult = await GetLockedBarNoByDocNosAsync(docNo, warehouseLocation);
                var existingLocks = existingLocksResult.Data?.OrderBy(x => x.CreateTime).ToList() ?? new List<LockedBarNo>();

                if (existingLocks.Count > 0)
                {
                    var existingItems = existingLocks.Select(x => new VariableItem
                    {
                        ProductNo = x.ProductNo,
                        BarNo = x.BarNo,
                        BarQty = x.BarQty,
                        RequiredQty = x.RequiredQty,
                        BinNo = x.BinNo
                    }).ToList();

                    await transaction.CommitAsync();
                    return Result<List<VariableItem>>.Ok(existingItems, $"已加载当前领料单锁定条码，共{existingItems.Count}条");
                }

                var fromGuidResult = await GetFromGuid(docNo);
                if (!fromGuidResult.Success || string.IsNullOrWhiteSpace(fromGuidResult.Data))
                {
                    await transaction.RollbackAsync();
                    return Result<List<VariableItem>>.Fail(fromGuidResult.Message ?? "未找到对应的领料单");
                }

                var details = await _context.PickDetails
                    .Where(x => x.FromGuid == fromGuidResult.Data)
                    .ToListAsync();

                if (details.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return Result<List<VariableItem>>.Fail("领料单没有明细数据");
                }

                var itemGuids = details
                    .Select(x => x.ItemGuid)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                var itemNoMap = await _context.itemDetails
                    .Where(x => itemGuids.Contains(x.Guid))
                    .ToDictionaryAsync(x => x.Guid, x => x.ItemNo ?? string.Empty);

                var selectedItems = new List<VariableItem>();
                var lockEntities = new List<LockedBarNo>();
                var selectedBarNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var detail in details)
                {
                    var requiredQty = detail.ApplyQty - detail.PickingQty;
                    if (requiredQty <= 0)
                    {
                        continue;
                    }

                    var itemGuid = detail.ItemGuid ?? string.Empty;
                    var productNo = itemNoMap.TryGetValue(itemGuid, out var mappedProductNo) ? mappedProductNo : string.Empty;

                    // 需求量remaining
                    decimal remaining = requiredQty;
                    // 分页查询大小
                    const int pageSize = 200;
                    var pageIndex = 0;
                    var selectedCountBefore = selectedItems.Count;

                    while (remaining > 0)
                    {
                        // 查询存在（排除当前仓库已锁定的条码）
                        var bars = await _context.BarDetails
                            .Where(x => x.ItemGuid == itemGuid
                                && x.IsRack == "Y"
                                && x.WarehouseNo == warehouseLocation
                                && x.EnableFlag == "Y"
                                && x.BarQty > 0
                                && x.isDelete == "N"
                                && !_context.LockedBarNos.Any(l => l.BarNo == x.BarNo && l.WarehouseLocation == warehouseLocation)
                                && !selectedBarNos.Contains(x.BarNo))
                            .OrderBy(x => x.InstockDate)
                            .Skip(pageIndex * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                        if (bars.Count == 0)
                        {
                            break;
                        }

                        foreach (var bar in bars)
                        {
                            if (string.IsNullOrWhiteSpace(bar.BarNo) || selectedBarNos.Contains(bar.BarNo))
                            {
                                continue;
                            }

                            selectedBarNos.Add(bar.BarNo);
                            selectedItems.Add(new VariableItem
                            {
                                ProductNo = productNo,
                                BarNo = bar.BarNo,
                                BarQty = bar.BarQty,
                                RequiredQty = requiredQty,
                                BinNo = bar.BinNo
                            });

                            lockEntities.Add(new LockedBarNo
                            {
                                Guid = Guid.NewGuid().ToString(),
                                ProductNo = productNo,
                                BarNo = bar.BarNo,
                                DocNo = docNo,
                                RequiredQty = requiredQty,
                                BarQty = bar.BarQty,
                                BinNo = bar.BinNo,
                                CreateTime = DateTime.Now,
                                Creator = "1f48d517-782e-4924-8c6f-f8a32269d910",
                                WarehouseLocation = warehouseLocation
                            });

                            remaining -= (bar.BarQty ?? 0m);
                            if (remaining <= 0)
                            {
                                break;
                            }
                        }

                        pageIndex++;
                    }

                    if (selectedItems.Count == selectedCountBefore)
                    {
                        selectedItems.Add(new VariableItem
                        {
                            ProductNo = productNo,
                            BarNo = null,
                            BarQty = null,
                            RequiredQty = requiredQty,
                            BinNo = null
                        });
                    }
                }

                if (lockEntities.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return Result<List<VariableItem>>.Fail("没有可分配并锁定的条码");
                }

                await _context.LockedBarNos.AddRangeAsync(lockEntities);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var hasUnallocatedItem = selectedItems.Any(x => string.IsNullOrWhiteSpace(x.BarNo));
                var message = hasUnallocatedItem
                    ? $"部分条码已锁定，共{lockEntities.Count}条，存在未分配项"
                    : $"查询并锁定成功，共{lockEntities.Count}条";

                return Result<List<VariableItem>>.Ok(selectedItems, message);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                return Result<List<VariableItem>>.Fail("存在条码已被其他终端锁定，请重新查询");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result<List<VariableItem>>.Fail($"查询并锁定失败：{ex.Message}");
            }
        }

        // 锁定条码
        public async Task<Result<bool>> LockBarsAsync(List<VariableItem> tokens, string docNo, string warehouseLocation)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return Result<bool>.Fail("无效的条码列表");
            }

            var lockedBars = tokens.Where(x => !string.IsNullOrWhiteSpace(x.BarNo)
                                            && !string.IsNullOrWhiteSpace(x.ProductNo)
                                            && !string.IsNullOrWhiteSpace(x.BinNo))
                                   .Select(tokens => new LockedBarNo
            {
                Guid = Guid.NewGuid().ToString(),
                ProductNo = tokens.ProductNo,
                BarNo = tokens.BarNo,
                DocNo = docNo,
                RequiredQty = tokens.RequiredQty,
                BarQty = tokens.BarQty,
                BinNo = tokens.BinNo,
                CreateTime = DateTime.Now,
                Creator = "1f48d517-782e-4924-8c6f-f8a32269d910",
                WarehouseLocation = warehouseLocation
            }).ToList();

            if (lockedBars.Count == 0)
            {
                return Result<bool>.Fail("没有可锁定的条码");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 幂等检查：过滤已存在的锁定条码，避免重复插入
                var barNosToLock = lockedBars.Select(x => x.BarNo).ToList();
                var alreadyLocked = await _context.LockedBarNos
                    .Where(x => x.DocNo == docNo && x.WarehouseLocation == warehouseLocation && barNosToLock.Contains(x.BarNo))
                    .Select(x => x.BarNo)
                    .ToHashSetAsync();

                var newBars = lockedBars.Where(x => !alreadyLocked.Contains(x.BarNo)).ToList();
                if (newBars.Count == 0)
                {
                    await transaction.CommitAsync();
                    return Result<bool>.Ok(true, "条码已锁定（幂等）");
                }

                await _context.LockedBarNos.AddRangeAsync(newBars);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Result<bool>.Ok(true, "锁定条码成功");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result<bool>.Fail($"锁定条码失败：{ex.Message}");
            }
        }

        // 解锁条码（按仓库过滤）
        public async Task<Result<bool>> UnLockBarsAsync(List<VariableItem> tokens, string docNo, string warehouseLocation)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return Result<bool>.Fail("无效的条码列表");
            }

            if (string.IsNullOrWhiteSpace(docNo))
            {
                return Result<bool>.Fail("领料单号不能为空");
            }

            if (string.IsNullOrWhiteSpace(warehouseLocation))
            {
                return Result<bool>.Fail("仓库编码不能为空");
            }

            var barNos = tokens
                .Select(t => t.BarNo)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (barNos.Count == 0)
            {
                return Result<bool>.Fail("未找到可解锁条码");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                 // 查询出锁定的条码列表（按仓库过滤）
                var lockedBars = await _context.LockedBarNos
                    .Where(x => x.DocNo == docNo
                        && x.WarehouseLocation == warehouseLocation
                        && barNos.Contains(x.BarNo))
                    .ToListAsync();

                if (lockedBars.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return Result<bool>.Fail("未找到需要解锁的条码记录");
                }

                _context.LockedBarNos.RemoveRange(lockedBars);
                var result = await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result<bool>.Ok(true, $"解锁条码完成，解除{result}个条码");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result<bool>.Fail($"解锁条码失败：{ex.Message}");
            }
        }

        // 查询领料单DocNo锁定的条码（按仓库过滤）
        public async Task<Result<List<LockedBarNo>>> GetLockedBarNoByDocNosAsync(string docNo, string warehouseLocation)
        {
            var lockedBars = await _context.LockedBarNos
                           .Where(x => x.DocNo == docNo && x.WarehouseLocation == warehouseLocation)
                           .ToListAsync();
            return Result<List<LockedBarNo>>.Ok(lockedBars, "查询锁定条码成功");
        }

        // 拣货完成：解除库位占用 + 清理锁定记录（按仓库过滤）
        public async Task<Result<bool>> CompletePickingAsync(string docNo, List<string> binNos, string warehouseLocation)
        {
            if (string.IsNullOrWhiteSpace(docNo) || binNos == null || binNos.Count == 0)
            {
                return Result<bool>.Fail("参数不完整");
            }

            if (string.IsNullOrWhiteSpace(warehouseLocation))
            {
                return Result<bool>.Fail("仓库编码不能为空");
            }

            var validBinNos = binNos.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (validBinNos.Count == 0)
            {
                return Result<bool>.Fail("无有效库位");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. 解除库位占用（加仓库过滤，防止不同仓库相同库位号被误释放）
                var shelves = await _context.ShelfDetails
                    .Where(x => validBinNos.Contains(x.BinNo) && x.WarehouseNo == warehouseLocation)
                    .ToListAsync();

                foreach (var shelf in shelves)
                {
                    shelf.IsEnable = "Y";
                }

                // 2. 清理锁定记录（按仓库过滤，复用公共方法）
                var lockedBarsResult = await GetLockedBarNoByDocNosAsync(docNo, warehouseLocation);
                var lockedBars = lockedBarsResult.Data ?? new List<LockedBarNo>();

                // 3. 更新条码状态为已出库（IsRack = 'N'）
                var barNos = lockedBars.Select(x => x.BarNo).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (barNos.Count > 0)
                {
                    var barsToUpdate = await _context.BarDetails
                        .Where(x => barNos.Contains(x.BarNo))
                        .ToListAsync();

                    foreach (var bar in barsToUpdate)
                    {
                        bar.IsRack = "N";
                    }
                }

                _context.LockedBarNos.RemoveRange(lockedBars);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result<bool>.Ok(true, $"拣货完成，解除{shelves.Count}个库位占用");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result<bool>.Fail($"拣货完成失败：{ex.Message}");
            }
        }
    }
}
