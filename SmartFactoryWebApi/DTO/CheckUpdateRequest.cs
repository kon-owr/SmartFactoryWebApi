namespace SmartFactoryWebApi.DTO
{
    public class CheckUpdateRequest
    {
        public string AppId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public int CurrentVersionCode { get; set; }
        public string? Channel { get; set; }
    }
}
