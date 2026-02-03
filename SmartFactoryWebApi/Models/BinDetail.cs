using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_BIN_DETAIL")]
    public class BinDetail
    {
        [Key]
        [Column("GUID")]
        public string Guid { get; set; }
        [Column("FROM_GUID")]
        public string FromGuid { get; set; }
        [Column("BIN_NO")]
        public string BinNo { get; set; }
    }
}
