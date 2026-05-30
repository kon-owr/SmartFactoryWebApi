using Microsoft.EntityFrameworkCore;
using SmartFactoryWebApi.Models;

namespace SmartFactoryWebApi.Data
{
    /// <summary>
    /// WMS 业务数据库上下文。
    /// </summary>
    public class WmsDbContext : DbContext
    {
        public WmsDbContext(DbContextOptions<WmsDbContext> options) : base(options)
        {
        }

        public WmsDbContext()
        {
        }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 锁定表要求同一条码只能存在一条有效锁记录。
            modelBuilder.Entity<LockedBarNo>()
                .HasIndex(x => x.BarNo)
                .IsUnique()
                .HasDatabaseName("UX_CUS_PICKING_LOCK_BARNO_BAR_NO")
                .HasFilter("[BAR_NO] IS NOT NULL");

            // 复合索引用于按单号回收锁定记录。
            modelBuilder.Entity<LockedBarNo>()
                .HasIndex(x => new { x.DocNo, x.BarNo })
                .HasDatabaseName("IX_CUS_PICKING_LOCK_BARNO_DOCNO_BARNO");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }
    }
}
