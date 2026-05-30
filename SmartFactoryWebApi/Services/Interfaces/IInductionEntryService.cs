using SmartFactoryWebApi.DTO;

namespace SmartFactoryWebApi.Services
{
    public interface IInductionEntryService
    {
        Task<Result<InductionShelfValidation>> ValidateShelfAsync(string shelfCode, string warehouseLocation);
        Task<Result<string>> DepositAsync(string barcode, string shelfCode, string warehouseLocation);
        Task<Result<string>> CancelDepositAsync(string barcode);
        Task<Result<string>> HandleDepositCallbackAsync(string labelId, string location, string? detailsJson);
    }
}
