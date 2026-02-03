namespace SmartFactoryWebApi.DTO
{
    public class GetBarsRequest
    {
        public decimal RequiredQty { get; set; }
        public string ItemGuid { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
    }
}