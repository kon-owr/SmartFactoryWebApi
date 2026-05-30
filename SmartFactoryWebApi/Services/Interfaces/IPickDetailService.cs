using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 定义普通拣货主链路的领域服务能力。
    /// </summary>
    public interface IPickDetailService
    {
        /// <summary>
        /// 根据领料单号查询明细。
        /// </summary>
        Task<Result<List<PickDetail>>> GetPickDetailAsync(string docNo);

        /// <summary>
        /// 校验领料单是否存在。
        /// </summary>
        Task<Result<bool>> isExistPickApplyDoc(string docno);

        /// <summary>
        /// 根据领料单号获取单头 GUID。
        /// </summary>
        Task<Result<string>> GetFromGuid(string docno);

        /// <summary>
        /// 根据物料 GUID 查询料号。
        /// </summary>
        Task<Result<string>> GetItemNoByItemGuid(string itemGuid);

        /// <summary>
        /// 按需求数量和仓库执行 FIFO 条码查询。
        /// </summary>
        Task<Result<List<BarDetail>>> GetBarByRequiredQtyQtyAndProductNo(decimal? needQty, string productNo, string warehouseLocation);

        /// <summary>
        /// 根据领料单查询并锁定当前仓库可用条码。
        /// </summary>
        Task<Result<List<VariableItem>>> ReserveBarsByDocNoAsync(string docNo, string warehouseLocation);

        /// <summary>
        /// 显式锁定条码。
        /// </summary>
        Task<Result<bool>> LockBarsAsync(List<VariableItem> barNoList, string docNo, string warehouseLocation);

        /// <summary>
        /// 显式解锁条码。
        /// </summary>
        Task<Result<bool>> UnLockBarsAsync(List<VariableItem> barNoList, string docNo, string warehouseLocation);

        /// <summary>
        /// 查询指定领料单在当前仓库下的锁定条码。
        /// </summary>
        Task<Result<List<LockedBarNo>>> GetLockedBarNoByDocNosAsync(string docNo, string warehouseLocation);

        /// <summary>
        /// 完成拣货并释放库位、锁定和条码状态。
        /// </summary>
        Task<Result<bool>> CompletePickingAsync(string docNo, List<string> binNos, string warehouseLocation);
    }
}
