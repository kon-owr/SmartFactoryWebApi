using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    /// <summary>
    /// 定义普通入库主链路的领域服务能力。
    /// </summary>
    public interface IEntryDetailService
    {
        /// <summary>
        /// 查询托盘码对应的托盘明细。
        /// </summary>
        Task<Result<IEnumerable<PalletDetail>>> GetPalletDetailsAsync(string code);

        /// <summary>
        /// 查询单个条码明细。
        /// </summary>
        Task<Result<IEnumerable<BarDetail>>> GetBarDetailsAsync(string code);

        /// <summary>
        /// 查询条码并分配库位，包含条码解析、库位可用性校验和分配结果。
        /// </summary>
        Task<Result<IEnumerable<PalletBarRelation>>> AllocateAsync(string barcode, string binNo);

        /// <summary>
        /// 将已分配条码正式入库，并同步库存和库位状态。
        /// </summary>
        Task<Result<IEnumerable<PalletBarRelation>>> CommitAsync(IEnumerable<PalletBarRelation> items, string warehouseLocation);
    }
}
