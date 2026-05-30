namespace SmartFactoryWebApi.DTO
{
    public class GetCommitBarDetails
    {
        public IEnumerable<PalletBarRelation> Items { get; init; }
        public string WarehouseLocation { get; init; }
    }
}
