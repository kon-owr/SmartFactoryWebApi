using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;
using System.Data;
using System.IO.Pipelines;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 处理普通拣货流程的领域逻辑。
    /// </summary>
    public class PickDetailService : IPickDetailService
    {
        /// <summary>
        /// 访问 WMS 领料单、条码、锁定记录和货架库位数据的数据库上下文。
        /// </summary>
        private readonly WmsDbContext _context;

        /// <summary>
        /// 普通拣货允许操作的仓库编码白名单。
        /// </summary>
        private readonly List<string> AllowWarehouseList = new() { "601", "617", "621" };

        /// <summary>
        /// 初始化普通拣货服务依赖的数据库上下文。
        /// </summary>
        public PickDetailService(WmsDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 根据领料单号查询领料明细。
        /// </summary>
        public async Task<Result<List<PickDetail>>> GetPickDetailAsync(string docNo)
        {
            var resultGuid = await GetFromGuid(docNo);
            if (!resultGuid.Success)
            {
                return Result<List<PickDetail>>.Fail("未找到对应的领料单");
            }

            var result = await _context.PickDetails
                           .Where(x => x.FromGuid == resultGuid.Data)
                           .ToListAsync();
            return Result<List<PickDetail>>.Ok(result, "查询领料单明细成功");
        }

        /// <summary>
        /// 校验领料单主表是否存在。
        /// </summary>
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

        /// <summary>
        /// 根据领料单号查询主表 GUID，供明细查询和锁定流程使用。
        /// </summary>
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

        /// <summary>
        /// 根据物料 GUID 查询料号。
        /// </summary>
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

        /// <summary>
        /// 按需求数量、物料和仓库用 FIFO 顺序查询可出库条码。
        /// </summary>
        public async Task<Result<List<BarDetail>>> GetBarByRequiredQtyQtyAndProductNo(decimal? needQty, string itemGuid, string warehouseLocation)
        {
            if (needQty <= 0 || string.IsNullOrWhiteSpace(itemGuid))
            {
                return Result<List<BarDetail>>.Fail("无效的查询参数");
            }

            // 当前服务只允许配置内的仓库参与拣货，避免误查到其他仓库条码。
            if (!AllowWarehouseList.Contains(warehouseLocation))
            {
                return Result<List<BarDetail>>.Fail($"仓库编码无效：{warehouseLocation}");
            }


            const int pageSize = 200;
            var selectedBars = new List<BarDetail>();
            var remaining = needQty;
            var pageIndex = 0;

            // 锁定记录必须按仓库隔离，否则会把其他仓库的锁误认为当前仓库不可用条码。
            var lockedBarNos = _context.LockedBarNos
                .Where(x => x.WarehouseLocation == warehouseLocation)
                .Select(x => x.BarNo)
                .ToHashSet();

            while (remaining > 0)
            {
                var query = _context.BarDetails
                                .Where(x =>
                                    x.ItemGuid == itemGuid &&
                                    x.IsRack == "Y" &&
                                    x.WarehouseNo == warehouseLocation &&
                                    x.EnableFlag == "Y" &&
                                    x.BarQty > 0 &&
                                    x.isDelete == "N");
                
                if(lockedBarNos.Count > 0)
                {
                    query = query.Where(x => !lockedBarNos.Contains(x.BarNo));
                }

                var orderedBars = await ApplyFifoOrdering(query)
                                        .Skip(pageIndex * pageSize)
                                        .Take(pageSize)
                                        .ToListAsync();

                if (!orderedBars.Any())
                {
                    break;
                }

                foreach (var bar in orderedBars)
                {
                    selectedBars.Add(bar);
                    remaining -= bar.BarQty ?? 0;
                    if (remaining <= 0) break;
                }

                pageIndex++;
            }

            return Result<List<BarDetail>>.Ok(selectedBars, "查询条码成功");
        }

        /// <summary>
        /// 按领料单在事务内分配并锁定当前仓库可出库条码。
        /// </summary>
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
                // 优先复用当前仓库下已有的锁定结果，避免重复分配造成并发冲突。
                var existingLocksResult = await GetLockedBarNoByDocNosAsync(docNo, warehouseLocation);
                var existingLocks = existingLocksResult.Data ?? new List<LockedBarNo>();

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
                
                // 查询领料单明细行数据
                var details = await _context.PickDetails
                    .Where(x => x.FromGuid == fromGuidResult.Data)
                    .ToListAsync();

                if (details.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return Result<List<VariableItem>>.Fail("领料单没有明细数据");
                }

                // 将领料行映射为料品GUID列表
                var itemGuids = details
                    .Select(x => x.ItemGuid)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                // 将领料行映射为料品GUID-料号的Map
                var itemNoMap = await _context.itemDetails
                    .Where(x => itemGuids.Contains(x.Guid))
                    .ToDictionaryAsync(x => x.Guid, x => x.ItemNo ?? string.Empty);

                var selectedItems = new List<VariableItem>();
                var lockEntities = new List<LockedBarNo>();
                var selectedBarNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 计算领料明细行需要分配的条码
                foreach (var detail in details)
                {
                    // 领料行未领料数量
                    var requiredQty = detail.ApplyQty - detail.PickingQty;
                    if (requiredQty <= 0)
                    {
                        continue;
                    }

                    var itemGuid = detail.ItemGuid ?? string.Empty;
                    var productNo = itemNoMap.TryGetValue(itemGuid, out var mappedProductNo) ? mappedProductNo : string.Empty;
                    
                    decimal remaining = requiredQty;
                    // 分页查询参数
                    const int pageSize = 200;
                    var pageIndex = 0;
                    var selectedCountBefore = selectedItems.Count;

                    while (remaining > 0)
                    {
                        // 每次分页都排除当前仓库已锁定条码和本次循环已选条码，避免重复入选。
                        var bars = await ApplyFifoOrdering(
                                _context.BarDetails
                                    .Where(x => x.ItemGuid == itemGuid
                                        && x.IsRack == "Y"
                                        && x.WarehouseNo == warehouseLocation
                                        && x.EnableFlag == "Y"
                                        && x.BarQty > 0
                                        && x.isDelete == "N"
                                        && !_context.LockedBarNos.Any(l => l.BarNo == x.BarNo && l.WarehouseLocation == warehouseLocation)
                                        && !selectedBarNos.Contains(x.BarNo)))
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

                            // 扣减领料行剩余未分配数量
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

                var hasUnallocatedItem = selectedItems.Any(x => string.IsNullOrWhiteSpace(x.BarNo));
                if (lockEntities.Count == 0)
                {
                    await transaction.RollbackAsync();
                    var failureMessage = hasUnallocatedItem
                        ? "领料单明细没有匹配到可出库条码"
                        : "没有可分配并锁定的条码";
                    return Result<List<VariableItem>>.Fail(failureMessage);
                }

                await _context.LockedBarNos.AddRangeAsync(lockEntities);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

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

        /// <summary>
        /// 显式锁定前端传入的一批条码，并支持重复锁定的幂等返回。
        /// </summary>
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
                // 手动锁定支持幂等调用，重复提交同一批条码不会重复插入记录。
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

        /// <summary>
        /// 按单号、仓库和条码列表释放对应锁定记录。
        /// </summary>
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
                // 解锁时必须带仓库条件，避免跨仓库误删同单号锁定记录。
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

        /// <summary>
        /// 查询指定领料单在当前仓库下的锁定条码，并按 FIFO 顺序返回。
        /// </summary>
        public async Task<Result<List<LockedBarNo>>> GetLockedBarNoByDocNosAsync(string docNo, string warehouseLocation)
        {
            var lockedBars = await _context.LockedBarNos
                           .Where(x => x.DocNo == docNo && x.WarehouseLocation == warehouseLocation)
                           .ToListAsync();
            var orderedLockedBars = await OrderLockedBarsByFifoAsync(lockedBars);
            return Result<List<LockedBarNo>>.Ok(orderedLockedBars, "查询锁定条码成功");
        }

        /// <summary>
        /// 完成普通拣货，释放库位占用、更新条码状态并清理锁定记录。
        /// </summary>
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
                // 完成拣货后，先释放当前仓库内涉及的库位占用。
                var shelves = await _context.ShelfDetails
                    .Where(x => validBinNos.Contains(x.BinNo) && x.WarehouseNo == warehouseLocation)
                    .ToListAsync();

                foreach (var shelf in shelves)
                {
                    shelf.IsEnable = "Y";
                }

                // 锁定记录与条码状态都按仓库隔离回收，避免影响其他仓库相同单号作业。
                var lockedBarsResult = await GetLockedBarNoByDocNosAsync(docNo, warehouseLocation);
                var lockedBars = lockedBarsResult.Data ?? new List<LockedBarNo>();

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

        /// <summary>
        /// 为可出库条码查询追加 FIFO 和料架物理顺序排序。
        /// </summary>
        private IQueryable<BarDetail> ApplyFifoOrdering(IQueryable<BarDetail> query)
        {
            return from bar in query
                   join shelf in _context.ShelfDetails.Where(x => x.DeleteFlag == "N")
                       on bar.BinNo equals shelf.BinNo into shelfGroup
                   from shelf in shelfGroup.DefaultIfEmpty()
                   let shelfNo = shelf != null ? shelf.ShelfNo : string.Empty
                   let row = shelf != null ? shelf.Row : int.MaxValue
                   let directionalColumn = shelf != null
                       ? ((shelf.SortDirection ?? "ASC") == "DESC" ? -shelf.Column : shelf.Column)
                       : int.MaxValue
                   orderby bar.InstockDate,
                           shelfNo,
                           row,
                           directionalColumn,
                           bar.BinNo,
                           bar.BarNo
                   select bar;
        }

        /// <summary>
        /// 将锁定记录按关联料架和创建时间排序，保持前端展示顺序稳定。
        /// </summary>
        private async Task<List<LockedBarNo>> OrderLockedBarsByFifoAsync(List<LockedBarNo> lockedBars)
        {
            if (lockedBars.Count <= 1)
            {
                return lockedBars;
            }

            var binNos = lockedBars
                .Select(x => x.BinNo)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var shelfMap = await _context.ShelfDetails
                .Where(x => binNos.Contains(x.BinNo) && x.DeleteFlag == "N")
                .ToDictionaryAsync(x => x.BinNo!, x => x);

            return lockedBars
                .Select(x =>
                {
                    shelfMap.TryGetValue(x.BinNo ?? string.Empty, out var shelf);
                    return new
                    {
                        LockedBar = x,
                        Shelf = shelf
                    };
                })
                .OrderBy(x => x.LockedBar.CreateTime)
                .ThenBy(x => x.Shelf?.ShelfNo ?? string.Empty)
                .ThenBy(x => x.Shelf?.Row ?? int.MaxValue)
                .ThenBy(x => GetDirectionalColumn(x.Shelf))
                .ThenBy(x => x.LockedBar.BinNo ?? string.Empty)
                .ThenBy(x => x.LockedBar.BarNo ?? string.Empty)
                .Select(x => x.LockedBar)
                .ToList();
        }

        /// <summary>
        /// 根据料架排序方向计算参与排序的列值。
        /// </summary>
        private static int GetDirectionalColumn(ShelfDetail? shelf)
        {
            if (shelf == null)
            {
                return int.MaxValue;
            }

            return string.Equals(shelf.SortDirection, "DESC", StringComparison.OrdinalIgnoreCase)
                ? -shelf.Column
                : shelf.Column;
        }
    }
}
