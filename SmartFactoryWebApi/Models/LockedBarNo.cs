using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("CUS_PICKING_LOCK_BARNO")]
    public class LockedBarNo
    {
        [Key]
        [Column("GUID")]
        public string? Guid { get; set; }
        [Column("PRODUCT_NO")]
        public string? ProductNo { get; set; }
        [Column("BAR_NO")]
        public string? BarNo { get; set; }
        [Column("BAR_QTY")]
        public decimal? BarQty { get; set; }
        [Column("REQUIRED_QTY")]
        public decimal RequiredQty { get; set; }
        [Column("BIN_NO")]
        public string? BinNo { get; set; }
        [Column("DOC_NO")]
        public string? DocNo { get; set; }
        [Column("CREATE_TIME")]
        public DateTime? CreateTime { get; set; }
        [Column("CREATOR")]
        public string? Creator { get; set; }
        [Column("WAREHOUSE_LOCATION")]
        public string? WarehouseLocation { get; set; }  // 仓库编码
    }
}
