using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Data
{
    public class WmsDbContext : DbContext
    {
        // 构造函数：允许 DI 容器传入 Options (比如连接字符串)
        public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
        {
            
        }

        // 或者保留无参构造函数用于设计时，但推荐使用 DI 配置
        // 这里演示最简单的重写 OnConfiguring 方式，兼容性强
        public WmsDbContext() { }

        // 定义 DbSet 属性，表示数据库中的表
        public DbSet<PickApply> PickApplies { get; set; }
        public DbSet<PickDetail> PickDetails { get; set; }
        public DbSet<ItemDetail> itemDetails { get; set; }
        public DbSet<BarDetail> BarDetails { get; set; }
        public DbSet<LockedBarNo> LockedBarNos { get; set; }
        public DbSet<PalletDetail> PalletDetails { get; set; }
        public DbSet<ShelfDetail> ShelfDetails { get; set; }
        public DbSet<WarehouseDetail> WarehouseDetails { get; set; }
        public DbSet<BinDetail> BinDetails { get; set; }
        public DbSet<StockDetail> StockDetails { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }
    }
}
