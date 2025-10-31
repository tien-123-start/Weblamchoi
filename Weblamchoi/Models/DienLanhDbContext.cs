using Microsoft.EntityFrameworkCore;

namespace weblamchoi.Models
{
    public class DienLanhDbContext : DbContext
    {
        public DienLanhDbContext(DbContextOptions<DienLanhDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Manufacturer> Manufacturers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Shipping> Shippings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<RevenueReport> RevenueReports { get; set; }
        public DbSet<ProductThumbnail> ProductThumbnails { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<BonusProduct> BonusProducts { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<UserVoucher> UserVouchers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Product>()
                .Property(p => p.OriginalPrice)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<RevenueReport>().ToTable("RevenueReport");
            modelBuilder.Entity<Admin>().ToTable("Admins", t => t.ExcludeFromMigrations());
            modelBuilder.Entity<Product>()
              .HasOne(p => p.BonusProduct)
              .WithMany(p => p.UsedAsBonusBy)
              .HasForeignKey(p => p.BonusProductID)
              .OnDelete(DeleteBehavior.SetNull); // Khi xóa sản phẩm quà thì không xóa sản phẩm chính






        }
    }
}
