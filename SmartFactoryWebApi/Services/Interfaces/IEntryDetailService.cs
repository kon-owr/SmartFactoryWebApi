using System.Collections.Generic;
using System.Threading.Tasks;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Services
{
    public interface IEntryDetailService
    {
        Task<Result<IEnumerable<PalletDetail>>> GetPalletDetailsAsync(string code);
        Task<Result<IEnumerable<BarDetail>>> GetBarDetailsAsync(string code);

        /// <summary>
        /// 查询条码并分配库位：包含条码解析、可用性校验、分配结果。
        /// </summary>
        Task<Result<IEnumerable<PalletBarRelation>>> AllocateAsync(string barcode, string shelf, string binNo);

        /// <summary>
        /// 将已分配的条码入库（更新条码库位、上架标记，并同步库存）。
        /// </summary>
        Task<Result<IEnumerable<PalletBarRelation>>> CommitAsync(IEnumerable<PalletBarRelation> items, string warehouseLocation);
    }
}
