using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_BAR_DETAIL")]
    public class BarDetail
    {
        [Key]
        [Column("GUID")]
        public string? Guid { get; set; }
        [Column("BAR_NO")]
        public string? BarNo { get; set; }
        [Column("ITEM_GUID")]
        public string? ItemGuid { get; set; }
        [Column("BAR_QTY")]
        public decimal? BarQty { get; set; }
        [Column("WAREHOUSE_NO")]
        public string? WarehouseNo { get; set; }
        [Column("BIN_NO")]
        public string? BinNo { get; set; }
        [Column("INSTOCK_DATE")]
        public DateTime? InstockDate { get; set; }
        [Column("IS_RACK")]
        public string? IsRack { get; set; }
        [Column("ENABLE_FLAG")]
        public string? EnableFlag { get; set; }
        [Column("LOT_NO")]
        public string? LotNo { get; set; }
        [Column("DELETE_FLAG")]
        public string? isDelete { get; set; }
    }
}
