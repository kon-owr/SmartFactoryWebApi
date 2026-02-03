using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    // 领料明细行
    [Table("WMS_PICKING_APPLY_DETAIL")]
    public class PickDetail
    {
        [Key] // 主键
        [Column("GUID")]
        public string? Guid { get; set; }
        [Column("FROM_GUID")]
        public string FromGuid { get; set; } = string.Empty; // 单头GUID
        [Column("ITEM_GUID")]
        public string ItemGuid { get; set; } = string.Empty; // 物料GUID
        [Column("APPLY_QTY")]
        public decimal ApplyQty { get; set; } // 库存数量
        [Column("PICKING_QTY")]
        public decimal PickingQty { get; set; } // 库存数量
        [Column("DELETE_FLAG")]
        public string? isDelete { get; set; }
    }
}
