using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Models;


namespace SmartFactoryWebApi.Services
{
    // Services/IStockService.cs
    public interface IPickDetailService
    {

        Task<Result<List<PickDetail>>> GetPickDetailAsync(string docNo);

        Task<Result<bool>> isExistPickApplyDoc(string docno);

        Task<Result<string>> GetFromGuid(string docno);

        Task<Result<string>> GetItemNoByItemGuid(string itemGuid);

        Task<Result<List<BarDetail>>> GetBarByRequiredQtyQtyAndProductNo(decimal? needQty, string productNo, string warehouseLocation);

        Task<Result<bool>> LockBarsAsync(List<VariableItem> barNoList, string docNo);
        Task<Result<bool>> UnLockBarsAsync(List<VariableItem> barNoList);
        Task<Result<List<LockedBarNo>>> GetLockedBarNoByDocNosAsync(string docNo);
    }
}
