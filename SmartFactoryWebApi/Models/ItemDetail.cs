using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_ITEM_DETAIL")]
    public class ItemDetail
    {
        [Key]
        [Column("GUID")]
        public string? Guid { get; set; }
        [Column("ITEM_NO")]
        public string ItemNo { get; set; } = string.Empty;
    }
}
