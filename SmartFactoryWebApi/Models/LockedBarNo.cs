using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFactoryWebApi.Models
{
    /// <summary>
    /// 拣货条码锁定记录。
    /// </summary>
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

        /// <summary>
        /// 仓库编码，用于隔离不同仓库的锁定记录。
        /// </summary>
        [Column("WAREHOUSE_LOCATION")]
        public string? WarehouseLocation { get; set; }
    }
}
