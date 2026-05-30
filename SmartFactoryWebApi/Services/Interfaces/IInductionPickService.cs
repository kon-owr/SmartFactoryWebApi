using SmartFactoryWebApi.DTO;

namespace SmartFactoryWebApi.Services
{
    public interface IInductionPickService
    {
        Task<Result<List<InductionPickItem>>> QueryByItemNoAsync(string itemNo, decimal? requiredQty, string warehouseLocation);
        Task<Result<List<string>>> GetItemSuggestionsAsync(string keyword, string warehouseLocation, int limit);
        Task<Result<string>> StartPickAsync(List<string> labelIds, string warehouseLocation, int color);
        Task<Result<string>> CancelPickAsync(List<string> labelIds);
        Task<Result<string>> HandlePickCallbackAsync(string labelId, string location, string? detailsJson);
    }
}
