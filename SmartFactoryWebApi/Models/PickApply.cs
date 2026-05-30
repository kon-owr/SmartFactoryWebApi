using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_PICKING_APPLY")]
    public class PickApply
    {
        [Key]
        [Column("GUID")]
        public string? Guid { get; set; }
        [Column("DOC_NO")]
        public string DocNo { get; set; } = string.Empty;
        [Column("DELETE_FLAG")]
        public string? isDelete { get; set; }
    }
}
