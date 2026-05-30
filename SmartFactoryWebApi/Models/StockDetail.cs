using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_ITEM_STOCK")]
    public class StockDetail
    {
        [Key]
        [Column("GUID")]
        public string Guid { get; set; }

        [Column("ITEM_GUID")]
        public string ItemGuid { get; set; }

        [Column("LOT_NO")]
        public string LotNo { get; set; }

        [Column("WAREHOUSE_GUID")]
        public string WarehouseGuid { get; set; }

        [Column("BIN_GUID")]
        public string BinGuid { get; set; }

        [Column("STOCK_QTY")]
        public decimal? StockQty { get; set; }

        [Column("LAST_RECEIPT_DATE")]
        public DateTime? LastReceiptDate { get; set; }

        [Column("LAST_QC_DATE")]
        public DateTime? LastQcDate { get; set; }

        [Column("PALLET_NO")]
        public string? PalletNo { get; set; }

        [Column("ORDER_NO")]
        public string? OrderNo { get; set; }

        [Column("ORDER_SEQ")]
        public string? OrderSeq { get; set; }

        [Column("CREATOR")]
        public string? Creator { get; set; }

        [Column("CREATE_TIME")]
        public DateTime? CreateTime { get; set; }

        [Column("FACTORY")]
        public string? Factory { get; set; }

        [Column("MODIFIER")]
        public string? Modifier { get; set; }

        [Column("MODIFY_TIME")]
        public DateTime? ModifyTime { get; set; }

        [Column("FLAG")]
        public decimal? Flag { get; set; }

        [Column("DELETE_FLAG")]
        public string? DeleteFlag { get; set; }
    }
}
