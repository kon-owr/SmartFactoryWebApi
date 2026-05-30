using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFactoryWebApi.Models
{
    /// <summary>
    /// 领料申请明细行。
    /// </summary>
    [Table("WMS_PICKING_APPLY_DETAIL")]
    public class PickDetail
    {
        [Key]
        [Column("GUID")]
        public string? Guid { get; set; }

        /// <summary>
        /// 对应领料单单头 GUID。
        /// </summary>
        [Column("FROM_GUID")]
        public string FromGuid { get; set; } = string.Empty;

        /// <summary>
        /// 物料 GUID。
        /// </summary>
        [Column("ITEM_GUID")]
        public string ItemGuid { get; set; } = string.Empty;

        /// <summary>
        /// 申请数量。
        /// </summary>
        [Column("APPLY_QTY")]
        public decimal ApplyQty { get; set; }

        /// <summary>
        /// 已拣数量。
        /// </summary>
        [Column("PICKING_QTY")]
        public decimal PickingQty { get; set; }

        [Column("DELETE_FLAG")]
        public string? isDelete { get; set; }
    }
}
