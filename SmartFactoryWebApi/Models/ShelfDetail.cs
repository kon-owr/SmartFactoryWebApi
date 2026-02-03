using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_SHELF_DETAIL")]
    public class ShelfDetail
    {
        [Key]
        [Column("GUID")]
        public string GUID { get; set; }
        [Column("SHELF_NO")]
        public string ShelfNo { get; set; }
        [Column("BIN_NO")]
        public string BinNo { get; set; }
        [Column("WAREHOUSE_NO")]
        public string WarehouseNo { get; set; }
        [Column("IS_ENABLE")]
        public string IsEnable { get; set; }
        [Column("IS_INDUCTION")]
        public string IsInduction { get; set; }
        [Column("ROW")]
        public int Row { get; set; }
        [Column("COLUMN")]
        public int Column { get; set; }
        [Column("DELETE_FLAG")]
        public string DeleteFlag { get; set; }
    }
}
