using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using weblamchoi.Models;
using weblamchoi.Services;

namespace weblamchoi.Controllers.Admin
{
    public class ProductsController : Controller
    {
        private readonly DienLanhDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ProductService _productService;

        public ProductsController(DienLanhDbContext context, IWebHostEnvironment environment, ProductService productService)
        {
            _context = context;
            _environment = environment;
            _productService = productService;
        }

        public async Task<IActionResult> Index()
        {
            var products = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Manufacturer)
                .Include(p => p.Thumbnails)
                .Include(p => p.BonusProduct);

            return View(await products.ToListAsync());
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Manufacturer)
                .Include(p => p.Thumbnails)
                .Include(p => p.BonusProduct)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            var relatedProducts = await _context.Products
                .Where(p => p.CategoryID == product.CategoryID && p.ProductID != product.ProductID)
                .Include(p => p.Thumbnails)
                .Take(8)
                .ToListAsync();

            var reviews = await _context.Reviews
                .Where(r => r.ProductID == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;
            ViewBag.Reviews = reviews;
            ViewBag.Categories = await _context.Categories.ToListAsync();

            // 🔑 Kiểm tra login
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool canComment = false;

            if (userId != null)
            {
                int userIdInt = int.Parse(userId);

                // Kiểm tra user đã mua sản phẩm thành công chưa
                bool hasPurchased = await _context.Orders
                    .Where(o => o.UserID == userIdInt && o.Status == "Thành công")
                    .SelectMany(o => o.OrderDetails)
                    .AnyAsync(od => od.ProductID == id);

                if (hasPurchased)
                {
                    // Kiểm tra user đã bình luận sản phẩm này chưa
                    bool alreadyReviewed = await _context.Reviews
                        .AnyAsync(r => r.ProductID == id && r.UserID == userIdInt);

                    canComment = !alreadyReviewed; // chỉ cho comment nếu chưa comment trước đó
                }
            }

            ViewBag.CanComment = canComment;

            // Kiểm tra BonusProduct còn hạn không
            if (product.BonusProduct != null)
            {
                if (product.BonusProduct.EndDate.HasValue &&
                    product.BonusProduct.EndDate.Value < DateTime.Now)
                {
                    product.BonusProduct = null; // hết hạn thì ẩn luôn
                }
            }

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> AddReview(int productId, string comment, int rating)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Users");

            int userIdInt = int.Parse(userId);

            // 🔍 Đếm số đơn hàng thành công mà user đã mua sản phẩm này
            int purchaseCount = await _context.Orders
                .Where(o => o.UserID == userIdInt && o.Status == "Thành công")
                .SelectMany(o => o.OrderDetails)
                .CountAsync(od => od.ProductID == productId);

            if (purchaseCount == 0)
            {
                TempData["Error"] = "Bạn cần mua sản phẩm này trước khi bình luận.";
                return RedirectToAction("Details", new { id = productId });
            }

            // 🔍 Đếm số bình luận đã có
            int reviewCount = await _context.Reviews
                .CountAsync(r => r.ProductID == productId && r.UserID == userIdInt);

            // Nếu số bình luận >= số lần mua -> không cho bình luận thêm
            if (reviewCount >= purchaseCount)
            {
                TempData["Error"] = "Bạn đã bình luận đủ số lần theo số lần mua.";
                return RedirectToAction("Details", new { id = productId });
            }

            // ✅ Nếu còn quyền thì thêm bình luận
            var review = new Review
            {
                ProductID = productId,
                UserID = userIdInt,
                Comment = comment,
                Rating = rating,
                ReviewDate = DateTime.Now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Bình luận thành công!";
            return RedirectToAction("Details", new { id = productId });
        }



        public async Task<IActionResult> Create()
        {
            var categories = await _context.Categories
                .Select(c => new SelectListItem { Value = c.CategoryID.ToString(), Text = c.CategoryName })
                .ToListAsync();
            var manufacturers = await _context.Manufacturers
                .Select(m => new SelectListItem { Value = m.ManufacturerID.ToString(), Text = m.ManufacturerName })
                .ToListAsync();
            var bonusProducts = await _productService.GetAvailableProductsAsync() ?? new List<Product>();

            if (!categories.Any())
            {
                TempData["Error"] = "Không có danh mục nào trong cơ sở dữ liệu. Vui lòng thêm danh mục trước.";
                return RedirectToAction(nameof(Index));
            }
            if (!manufacturers.Any())
            {
                TempData["Error"] = "Không có nhà sản xuất nào trong cơ sở dữ liệu. Vui lòng thêm nhà sản xuất trước.";
                return RedirectToAction(nameof(Index));
            }

            var model = new ProductCreateViewModel
            {
                Product = new Product(),
                Categories = categories,
                Manufacturers = manufacturers,
                BonusProducts = bonusProducts
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateViewModel model, IFormFile ImageFile, IFormFile[] ThumbnailFiles)
        {
            ModelState.Remove("Categories");
            ModelState.Remove("Manufacturers");
            ModelState.Remove("BonusProducts");

            if (model.Product.CategoryID != 0 && !await _context.Categories.AnyAsync(c => c.CategoryID == model.Product.CategoryID))
            {
                ModelState.AddModelError("Product.CategoryID", "Danh mục không tồn tại.");
            }

            if (model.Product.ManufacturerID.HasValue &&
                !await _context.Manufacturers.AnyAsync(m => m.ManufacturerID == model.Product.ManufacturerID.Value))
            {
                ModelState.AddModelError("Product.ManufacturerID", "Nhà sản xuất không tồn tại.");
            }

            if (model.Product.BonusProductID.HasValue &&
                !await _context.Products.AnyAsync(p => p.ProductID == model.Product.BonusProductID.Value))
            {
                ModelState.AddModelError("Product.BonusProductID", "Sản phẩm khuyến mãi không tồn tại.");
            }

            if (ModelState.IsValid)
            {
                var product = model.Product;

                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", uniqueFileName);

                    using (var stream = new FileStream(imagePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(stream);
                    }

                    product.ImageURL = "/uploads/" + uniqueFileName;
                }

                try
                {
                    _context.Products.Add(product);
                    await _context.SaveChangesAsync();

                    if (ThumbnailFiles != null && ThumbnailFiles.Any())
                    {
                        foreach (var file in ThumbnailFiles)
                        {
                            if (file != null && file.Length > 0)
                            {
                                var thumbFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                                var thumbPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/products/thumbnails", thumbFileName);

                                using (var stream = new FileStream(thumbPath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                var thumbnail = new ProductThumbnail
                                {
                                    ProductID = product.ProductID,
                                    ThumbnailURL = "/images/products/thumbnails/" + thumbFileName
                                };

                                _context.ProductThumbnails.Add(thumbnail);
                            }
                        }

                        await _context.SaveChangesAsync();
                    }

                    TempData["Success"] = "Tạo sản phẩm thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    TempData["Error"] = $"Lỗi khi lưu sản phẩm: {ex.InnerException?.Message ?? ex.Message}";
                }
            }
            else
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                TempData["Error"] = "Dữ liệu không hợp lệ: " + string.Join("; ", errors);
            }

            model.Categories = await _context.Categories
                .Select(c => new SelectListItem { Value = c.CategoryID.ToString(), Text = c.CategoryName })
                .ToListAsync() ?? new List<SelectListItem>();

            model.Manufacturers = await _context.Manufacturers
                .Select(m => new SelectListItem { Value = m.ManufacturerID.ToString(), Text = m.ManufacturerName })
                .ToListAsync() ?? new List<SelectListItem>();

            model.BonusProducts = await _productService.GetAvailableProductsAsync() ?? new List<Product>();

            return View(model);
        }
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.Thumbnails)
                .Include(p => p.BonusProduct)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null) return NotFound();

            ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", product.CategoryID);
            ViewData["ManufacturerID"] = new SelectList(_context.Manufacturers, "ManufacturerID", "ManufacturerName", product.ManufacturerID);

            ViewBag.BonusProducts = await _context.Products
                .Where(p => p.ProductID != id)
                .ToListAsync();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile ImageFile, IFormFileCollection ThumbnailFiles, int? SelectedBonusProductID)
        {
            if (id != product.ProductID)
                return NotFound();

            var existingProduct = await _context.Products
                .Include(p => p.Thumbnails)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (existingProduct == null)
                return NotFound();

            try
            {
                // Cập nhật thông tin sản phẩm
                if (existingProduct.ProductName != product.ProductName ||
                    existingProduct.Price != product.Price ||
                    existingProduct.Quantity != product.Quantity ||
                    existingProduct.Description != product.Description ||
                    existingProduct.CategoryID != product.CategoryID ||
                    existingProduct.ManufacturerID != product.ManufacturerID ||
                    existingProduct.IsAvailable != product.IsAvailable)
                {
                    existingProduct.ProductName = product.ProductName;
                    existingProduct.Price = product.Price;
                    existingProduct.Quantity = product.Quantity;
                    existingProduct.Description = product.Description;
                    existingProduct.CategoryID = product.CategoryID;
                    existingProduct.ManufacturerID = product.ManufacturerID;
                    existingProduct.IsAvailable = product.IsAvailable;
                }

                // Xử lý ảnh đại diện
                if (ImageFile != null && IsImageFile(ImageFile))
                {
                    if (!string.IsNullOrEmpty(existingProduct.ImageURL))
                        DeleteImageFile(existingProduct.ImageURL);

                    existingProduct.ImageURL = await SaveMainImageFile(ImageFile);
                }

                // Xử lý ảnh thumbnail
                if (ThumbnailFiles != null && ThumbnailFiles.Count > 0)
                {
                    foreach (var thumb in existingProduct.Thumbnails)
                    {
                        DeleteImageFile(thumb.ThumbnailURL);
                        _context.ProductThumbnails.Remove(thumb);
                    }

                    await SaveThumbnails(existingProduct.ProductID, ThumbnailFiles);
                }

                // Xử lý sản phẩm khuyến mãi
                if (SelectedBonusProductID.HasValue)
                {
                    var bonusProduct = await _context.Products.FindAsync(SelectedBonusProductID.Value);
                    if (bonusProduct != null)
                    {
                        existingProduct.BonusProductID = bonusProduct.ProductID;
                        existingProduct.StartDate = product.StartDate ?? DateTime.Now;
                        existingProduct.EndDate = product.EndDate ?? DateTime.Now.AddMonths(1);
                    }
                }
                else
                {
                    existingProduct.BonusProductID = null;
                    existingProduct.StartDate = null;
                    existingProduct.EndDate = null;
                }

                _context.Update(existingProduct);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật sản phẩm thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;

                ViewData["CategoryID"] = new SelectList(_context.Categories, "CategoryID", "CategoryName", product.CategoryID);
                ViewData["ManufacturerID"] = new SelectList(_context.Manufacturers, "ManufacturerID", "ManufacturerName", product.ManufacturerID);
                ViewBag.BonusProducts = await _context.Products.Where(p => p.ProductID != id).ToListAsync();

                return View(product);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBonusProduct(int productId, int bonusProductId, DateTime? startDate, DateTime? endDate)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var bonusProduct = await _context.Products.FindAsync(bonusProductId);
            if (bonusProduct == null) return NotFound();

            product.BonusProductID = bonusProduct.ProductID;
            product.StartDate = startDate ?? DateTime.Now;
            product.EndDate = endDate ?? DateTime.Now.AddMonths(1);

            _context.Update(product);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xác nhận và lưu sản phẩm khuyến mãi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Thumbnails)
                    .Include(p => p.UsedAsBonusBy)
                    .FirstOrDefaultAsync(p => p.ProductID == id);

                if (product == null)
                    return NotFound();

                // 🔍 Kiểm tra sản phẩm có trong OrderDetails không
                bool isInOrder = await _context.OrderDetails.AnyAsync(od => od.ProductID == id);
                if (isInOrder)
                {
                    TempData["Error"] = "Sản phẩm này đã được khách hàng mua, không thể xóa. Vui lòng đổi sang trạng thái ngừng bán.";
                    return RedirectToAction(nameof(Index));
                }

                if (product.UsedAsBonusBy != null && product.UsedAsBonusBy.Any())
                {
                    foreach (var p in product.UsedAsBonusBy)
                    {
                        p.BonusProductID = null;
                    }
                }

                if (!string.IsNullOrEmpty(product.ImageURL))
                    DeleteImageFile(product.ImageURL);

                foreach (var thumb in product.Thumbnails)
                {
                    DeleteImageFile(thumb.ThumbnailURL);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Xóa sản phẩm thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi xóa: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        public async Task<IActionResult> DeleteThumbnail(int thumbnailId)
        {
            try
            {
                var thumbnail = await _context.ProductThumbnails.FindAsync(thumbnailId);
                if (thumbnail == null)
                    return Json(new { success = false, message = "Không tìm thấy ảnh." });

                DeleteImageFile(thumbnail.ThumbnailURL);
                _context.ProductThumbnails.Remove(thumbnail);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Discount(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.ProductID == id);
            return product == null ? NotFound() : View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Discount(Product product)
        {
            try
            {
                var existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == product.ProductID);
                if (existing == null)
                {
                    TempData["Error"] = "Không tìm thấy sản phẩm!";
                    return View(product);
                }

                if (existing.OriginalPrice == null || existing.OriginalPrice == 0)
                    existing.OriginalPrice = existing.Price;

                existing.Price = product.Price;
                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật giá thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
                return View(product);
            }
        }
        [HttpPost]
        public IActionResult DiscountByPercent(int ProductID, double Price)
        {
            var product = _context.Products.FirstOrDefault(p => p.ProductID == ProductID);
            if (product == null) return NotFound();

            product.Price = (decimal)Price;
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        private async Task<string> SaveMainImageFile(IFormFile file)
        {
            var folder = Path.Combine(_environment.WebRootPath, "Uploads");
            Directory.CreateDirectory(folder);

            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var path = Path.Combine(folder, fileName);

            using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/Uploads/{fileName}".Replace("\\", "/");
        }

        private async Task SaveThumbnails(int productId, IFormFileCollection thumbnailFiles)
        {
            if (thumbnailFiles == null || thumbnailFiles.Count == 0) return;

            var folder = Path.Combine(_environment.WebRootPath, "images", "products", "thumbnails");
            Directory.CreateDirectory(folder);

            foreach (var file in thumbnailFiles)
            {
                if (file.Length > 0 && IsImageFile(file))
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                    var path = Path.Combine(folder, fileName);
                    using var stream = new FileStream(path, FileMode.Create);
                    await file.CopyToAsync(stream);

                    var url = $"images/products/thumbnails/{fileName}".Replace("\\", "/");
                    _context.ProductThumbnails.Add(new ProductThumbnail { ProductID = productId, ThumbnailURL = url });
                }
            }

            await _context.SaveChangesAsync();
        }
        private void SafeDeleteImage(Product product)
        {
            // Xử lý ảnh chính
            if (!string.IsNullOrEmpty(product.ImageURL))
            {
                var fullPath = Path.Combine(_environment.WebRootPath, product.ImageURL.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
                else
                {
                    // Nếu file không tồn tại thì clear luôn DB link
                    product.ImageURL = null;
                }
            }

            // Xử lý thumbnails
            if (product.Thumbnails != null && product.Thumbnails.Any())
            {
                foreach (var thumb in product.Thumbnails.ToList())
                {
                    var thumbPath = Path.Combine(_environment.WebRootPath, thumb.ThumbnailURL.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
                    if (System.IO.File.Exists(thumbPath))
                    {
                        System.IO.File.Delete(thumbPath);
                    }
                    else
                    {
                        // Nếu file không còn thì xóa record thumbnail luôn
                        _context.ProductThumbnails.Remove(thumb);
                    }
                }
            }
        }

        private bool IsImageFile(IFormFile file)
        {
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".jfif" };
            return allowedExts.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());
        }

        private void DeleteImageFile(string filePath)
        {
            var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.OrderDetails)
                    .ThenInclude(od => od.Order)
                .Include(p => p.Thumbnails)
                .Include(p => p.UsedAsBonusBy)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            // ❌ Nếu sản phẩm đang nằm trong giỏ hàng thì không cho xóa
            bool existsInCart = await _context.Carts.AnyAsync(c => c.ProductID == product.ProductID);
            if (existsInCart)
            {
                TempData["ErrorMessage"] = "❌ Không thể xóa sản phẩm vì vẫn còn trong giỏ hàng của khách.";
                return RedirectToAction(nameof(Index));
            }

            // Kiểm tra sản phẩm đã từng có đơn hàng chưa
            bool hasOrders = product.OrderDetails != null && product.OrderDetails.Any();

            if (hasOrders)
            {
                // Có đơn hàng liên quan -> kiểm tra trạng thái
                bool hasActiveOrders = product.OrderDetails
                    .Any(od => od.Order.Status != "Thành công" && od.Order.Status != "Đã hủy");

                if (hasActiveOrders)
                {
                    TempData["ErrorMessage"] = "❌ Sản phẩm đã có người mua và còn đơn hàng chưa hoàn tất, không thể xóa!";
                    return RedirectToAction(nameof(Index));
                }
            }

            try
            {
                // Nếu sản phẩm đang được gán làm Bonus cho sản phẩm khác thì gỡ ra
                if (product.UsedAsBonusBy != null && product.UsedAsBonusBy.Any())
                {
                    foreach (var p in product.UsedAsBonusBy)
                    {
                        p.BonusProductID = null;
                    }
                }

                // Xóa ảnh (hàm riêng xử lý nếu ảnh không tồn tại thì bỏ qua)
                SafeDeleteImage(product);

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                // Thông báo khác nhau
                if (!hasOrders)
                {
                    TempData["SuccessMessage"] = "✅ Xóa sản phẩm thành công (sản phẩm chưa từng có người mua).";
                }
                else
                {
                    TempData["SuccessMessage"] = "✅ Xóa sản phẩm thành công (sản phẩm đã từng có người mua nhưng tất cả đơn hàng đã hoàn tất/hủy).";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "❌ Lỗi khi xóa: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Index));
        }

    }
}