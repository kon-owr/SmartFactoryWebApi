using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Data;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;

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
                throw new ArgumentException("Invalid warehouse location.");
            }


            const int pageSize = 200; // 分页查询：可按实际调优
            var selectedBars = new List<BarDetail>();
            var remaining = needQty;
            var pageIndex = 0;

            // 获取当前所有锁定条码
            var lockedBarNos = _context.LockedBarNos.Select(x => x.BarNo).ToHashSet();

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
                    remaining -= bar.BarQty;
                    if (remaining <= 0) break;
                }

                pageIndex++; // 增加页码以继续查询下一页
            }

            return Result<List<BarDetail>>.Ok(selectedBars, "查询条码成功");
        }

        // 锁定条码
        public async Task<Result<bool>> LockBarsAsync(List<VariableItem> tokens, string docNo)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return Result<bool>.Fail("无效的条码列表");
            }

            var lockedBars = tokens.Where(x => string.IsNullOrEmpty(x.BarNo) 
                                            && string.IsNullOrEmpty(x.ProductNo)
                                            && string.IsNullOrEmpty(x.BinNo))
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
                // TODO
                Creator = "1f48d517-782e-4924-8c6f-f8a32269d910"
            }).ToList();

            await _context.LockedBarNos.AddRangeAsync(lockedBars);
            var result = await _context.SaveChangesAsync();
            if(result > 0)
            {   
                return Result<bool>.Ok(true, "锁定条码成功");
            }
            return Result<bool>.Fail("锁定条码失败");
        }

        // 解锁条码
        public async Task<Result<bool>> UnLockBarsAsync(List<VariableItem> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return Result<bool>.Fail("无效的条码列表");
            }

            // 查询出锁定的条码列表
            var lockedBars = await _context.LockedBarNos
                .Where(x => tokens.Select(t => t.BarNo).Contains(x.BarNo))
                .ToListAsync();

            if (lockedBars.Any())
            {
                _context.LockedBarNos.RemoveRange(lockedBars);
                var result = await _context.SaveChangesAsync();
                if (result > 0)
                {
                    return Result<bool>.Ok(true, "解锁条码成功");
                }
            }
            return Result<bool>.Fail("解锁条码失败");
        }

        // 查询领料单DocNo锁定的条码
        public async Task<Result<List<LockedBarNo>>> GetLockedBarNoByDocNosAsync(string docNo)
        {
            var lockedBars = await _context.LockedBarNos
                           .Where(x => x.DocNo == docNo)
                           .ToListAsync();
            return Result<List<LockedBarNo>>.Ok(lockedBars, "查询锁定条码成功");
        }
    }
}
