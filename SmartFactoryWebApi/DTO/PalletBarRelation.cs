using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartFactoryWebApi.DTO
{
    public class PalletBarRelation
    {
        public string? PalletNo { get; init; }
        public string? BarNo { get; init; }
        public string? BinNo { get; init; }
        public string? ShelfNo { get; init; }
    }
}
