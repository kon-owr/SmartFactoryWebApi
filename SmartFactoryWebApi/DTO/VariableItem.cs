namespace SmartFactoryWebApi.DTO
{
    public class VariableItem()
    {
        public string? ProductNo { get; set; }
        public string? BarNo { get; set; }
        public decimal? BarQty { get; set; }
        public decimal RequiredQty { get; set; }
        public string? BinNo { get; set; }


        public VariableItem(string productNo, string barNo, decimal? barQty, decimal requiredQty, string binNo) : this()
        {
            ProductNo = productNo;
            BarNo = barNo;
            BarQty = barQty;
            RequiredQty = requiredQty;
            BinNo = binNo;
        }
    }
}
