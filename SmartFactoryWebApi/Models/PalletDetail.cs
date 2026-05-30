using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.Models
{
    [Table("WMS_PALLET_STATE")]
    public class PalletDetail
    {
        [Key]
        [Column("GUID")]
        public string Guid { get; set; }
        [Column("PALLET_NO")]
        public string? PalletNo { get; set; }
        [Column("BAR_NO")]
        public string? BarNo { get; set; }
    }
}
