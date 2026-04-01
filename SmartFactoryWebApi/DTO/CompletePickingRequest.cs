namespace SmartFactoryWebApi.DTO
{
    public class CompletePickingRequest
    {
        public string DocNo { get; set; } = string.Empty;
        public List<string> BinNos { get; set; } = new();
        public string WarehouseLocation { get; set; } = string.Empty;
    }
}
