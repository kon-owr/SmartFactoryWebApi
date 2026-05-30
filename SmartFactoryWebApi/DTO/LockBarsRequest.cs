namespace SmartFactoryWebApi.DTO
{
    public class LockBarsRequest
    {
        public List<VariableItem> BarNolist { get; set; }
        public string DocNo { get; set; }
        public string WarehouseLocation { get; set; } = string.Empty;
    }
}
