using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_WAREHOUSE_DETAIL")]
    public class WarehouseDetail
    {
        [Key]
        [Column("GUID")]
        public string Guid { get; set; }
        [Column("WAREHOUSE_NO")]
        public string WarehouseNo { get; set; }
        [Column("DELETE_FLAG")]
        public string DeleteFlag { get; set; }
    }
}
