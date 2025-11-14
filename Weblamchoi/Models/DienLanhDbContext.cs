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
        public DbSet<MomoResponseEntity> MomoResponses { get; set; }
        public DbSet<MomoTransaction> MomoTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === TẤT CẢ CÁC TRƯỜNG DECIMAL ===
            modelBuilder.Entity<BonusProduct>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Cart>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<MomoTransaction>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(p => p.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderDetail>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderDetail>()
                .Property(p => p.UnitPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.PaidAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Product>()
                .Property(p => p.DiscountPercentage)
                .HasColumnType("decimal(5,2)");
                modelBuilder.Entity<Product>()
        .Property(p => p.Price)
        .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Product>()
                .Property(p => p.OriginalPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<RevenueReport>()
                .Property(p => p.TotalRevenue)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Shipping>()
                .Property(p => p.ShippingFee)
                .HasColumnType("decimal(18,2)");
        }
    }
}
