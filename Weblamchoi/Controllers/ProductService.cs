using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using weblamchoi.Models;
using Microsoft.EntityFrameworkCore;

namespace weblamchoi.Services
{
    public class ProductService
    {
        private readonly DienLanhDbContext _context;

        public ProductService(DienLanhDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // Phương thức mới để lấy danh sách sản phẩm khả dụng
        public async Task<List<Product>> GetAvailableProductsAsync()
        {
            return await _context.Products
                .Where(p => p.IsAvailable)
                .ToListAsync();
        }

        public async Task<bool> AddBonusProductAsync(int mainProductId, int bonusProductId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var mainProduct = await _context.Products.FindAsync(mainProductId);
                var bonusProduct = await _context.Products.FindAsync(bonusProductId);

                if (mainProduct == null || bonusProduct == null || !mainProduct.IsAvailable || !bonusProduct.IsAvailable)
                    return false;

                mainProduct.BonusProductID = bonusProduct.ProductID;
                mainProduct.StartDate = startDate ?? DateTime.Now;
                mainProduct.EndDate = endDate ?? DateTime.Now.AddMonths(1);

                _context.Products.Update(mainProduct);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveBonusProductAsync(int productId)
        {
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                    return false;

                // Xoá ngày dù có BonusProductID hay không
                product.BonusProductID = null;
                product.StartDate = null;
                product.EndDate = null;


                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Product> GetBonusProductAsync(int productId)
        {
            var bonus = await _context.BonusProducts
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.ProductID == productId);

            if (bonus?.Product != null)
            {
                // Gán ngày từ BonusProduct sang Product để hiển thị
                bonus.Product.StartDate = bonus.StartDate;
                bonus.Product.EndDate = bonus.EndDate;
                return bonus.Product;
            }

            return null;
        }


    }
}