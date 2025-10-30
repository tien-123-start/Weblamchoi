using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using System.Security.Claims;
using weblamchoi.Hubs; // namespace chứa NotificationHub

namespace weblamchoi.Controllers
{
    public class CartController : Controller
    {
        private readonly DienLanhDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public CartController(DienLanhDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string voucherCode)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");
            int userIdInt = int.Parse(userId);

            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive && v.EndDate >= DateTime.Now);

            if (voucher == null)
            {
                TempData["VoucherError"] = "Mã giảm giá không hợp lệ hoặc đã hết hạn.";
                return RedirectToAction("Index");
            }

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            decimal totalAmount = cartItems.Sum(c => (c.Price ?? c.Product?.Price ?? 0) * c.Quantity);
            decimal discount = voucher.IsPercentage
                ? totalAmount * (voucher.DiscountAmount / 100m)
                : Math.Min(voucher.DiscountAmount, totalAmount);

            // ✅ QUAN TRỌNG: Lưu voucher code và discount vào TempData với string format
            TempData["VoucherCode"] = voucherCode;
            TempData["VoucherDiscount"] = discount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); // Lưu string
            TempData["VoucherMessage"] = $"Áp dụng mã giảm giá thành công: {voucherCode} (Giảm {discount:N0} đ)";

            TempData.Keep("VoucherCode"); // Đảm bảo voucher code persist
            TempData.Keep("VoucherDiscount");

            return RedirectToAction("Index");
        }
        public async Task<IActionResult> IndexAsync(string voucherCode = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");
            int userIdInt = int.Parse(userId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userIdInt);
            ViewBag.UserPoints = user?.Points ?? 0;

            var cartItems = _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToList();

            decimal totalAmount = cartItems.Sum(c => (c.Price ?? c.Product?.Price ?? 0) * c.Quantity);

            // ✅ Kiểm tra voucher từ TempData trước
            if (string.IsNullOrEmpty(voucherCode))
            {
                voucherCode = TempData["VoucherCode"] as string;
                if (!string.IsNullOrEmpty(voucherCode))
                {
                    TempData.Keep("VoucherCode"); // Keep voucher code for next requests
                }
            }

            decimal discount = 0;
            if (!string.IsNullOrEmpty(voucherCode))
            {
                // ✅ Ưu tiên dùng discount đã tính sẵn từ ApplyVoucher
                var savedDiscount = TempData["VoucherDiscount"] as string;
                if (!string.IsNullOrEmpty(savedDiscount) && decimal.TryParse(savedDiscount, out decimal parsedDiscount))
                {
                    discount = parsedDiscount;
                }
                else
                {
                    // Fallback: Tính lại discount từ voucher
                    var voucher = _context.Vouchers
                        .FirstOrDefault(v => v.Code == voucherCode && v.IsActive && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now);
                    if (voucher != null)
                    {
                        discount = voucher.IsPercentage
                            ? totalAmount * (voucher.DiscountAmount / 100m)
                            : Math.Min(voucher.DiscountAmount, totalAmount);
                    }
                }
            }

            decimal finalAmount = Math.Max(totalAmount - discount, 0);

            // ✅ Set ViewBag values để view sử dụng
            ViewBag.TotalAmount = totalAmount;
            ViewBag.DiscountAmount = discount;
            ViewBag.TotalAfterDiscount = finalAmount;
            ViewData["AppliedVoucherCode"] = voucherCode;

            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> Add(int productId, int quantity = 1, decimal price = 0, int? bonusProductId = null, decimal bonusPrice = 0, bool applyDiscount = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                TempData["CartError"] = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.";
                return RedirectToAction("Index", "Login");
            }
            int userIdInt = int.Parse(userId);

            var product = await _context.Products
                .Include(p => p.BonusProduct)
                .FirstOrDefaultAsync(p => p.ProductID == productId);

            if (product == null)
            {
                TempData["CartError"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            Console.WriteLine($"Received - ProductID: {productId}, Price: {price}, applyDiscount: {applyDiscount}, bonusProductId: {bonusProductId}, bonusPrice: {bonusPrice}");

            decimal productPrice = applyDiscount ? price : product.Price;

            var existingItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserID == userIdInt && c.ProductID == productId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                existingItem.Price = productPrice;
                _context.Carts.Update(existingItem);
            }
            else
            {
                _context.Carts.Add(new Cart
                {
                    UserID = userIdInt,
                    ProductID = productId,
                    Quantity = quantity,
                    Price = productPrice,
                    AddedAt = DateTime.Now
                });
            }

            if (applyDiscount && bonusProductId.HasValue)
            {
                var bonusProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductID == bonusProductId.Value);

                if (bonusProduct != null)
                {
                    var existingBonusItem = await _context.Carts
                        .FirstOrDefaultAsync(c => c.UserID == userIdInt && c.ProductID == bonusProductId.Value);

                    if (existingBonusItem != null)
                    {
                        existingBonusItem.Quantity += quantity;
                        existingBonusItem.Price = bonusPrice;
                        _context.Carts.Update(existingBonusItem);
                    }
                    else
                    {
                        _context.Carts.Add(new Cart
                        {
                            UserID = userIdInt,
                            ProductID = bonusProductId.Value,
                            Quantity = quantity,
                            Price = bonusPrice,
                            AddedAt = DateTime.Now
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.User(userIdInt.ToString()).SendAsync("ReceiveCartUpdate", "Đã thêm sản phẩm vào giỏ hàng.");
            TempData["SuccessMessage"] = "Đã thêm sản phẩm vào giỏ hàng.";
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartId, string action)
        {
            var cartItem = await _context.Carts
                .Include(c => c.Product)
                .FirstOrDefaultAsync(c => c.CartID == cartId);
            if (cartItem == null) return NotFound();

            if (action == "increase") cartItem.Quantity++;
            else if (action == "decrease" && cartItem.Quantity > 1) cartItem.Quantity--;

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(string voucherCode, string paymentMethod, bool usePoints = false)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
                return RedirectToAction("Login", "Users");

            if (!int.TryParse(userIdClaim, out int userIdInt))
                return RedirectToAction("Login", "Users");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userIdInt);
            if (user == null)
                return RedirectToAction("Login", "Users");

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Tính tổng tiền
                decimal totalAmount = cartItems.Sum(item =>
                    (item.Price ?? item.Product?.Price ?? 0m) * item.Quantity);

                decimal discountAmount = 0;
                Voucher voucher = null;

                if (!string.IsNullOrEmpty(voucherCode))
                {
                    voucher = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.Code == voucherCode
                            && v.IsActive
                            && v.StartDate <= DateTime.Now
                            && v.EndDate >= DateTime.Now);

                    if (voucher == null)
                    {
                        TempData["VoucherMessage"] = "Mã voucher không hợp lệ hoặc đã hết hạn.";
                        return RedirectToAction("Index");
                    }

                    discountAmount = voucher.IsPercentage
                        ? totalAmount * (voucher.DiscountAmount / 100m)
                        : Math.Min(voucher.DiscountAmount, totalAmount);
                }

                decimal finalAmount = totalAmount - discountAmount;

                // Trừ điểm
                int pointsUsed = 0;
                decimal pointValue = 0;
                if (usePoints && user.Points > 0)
                {
                    const decimal pointRate = 1000m;
                    pointValue = Math.Min(user.Points * pointRate, finalAmount);
                    pointsUsed = (int)(pointValue / pointRate);
                    finalAmount -= pointValue;
                    user.Points -= pointsUsed;
                    _context.Users.Update(user);
                }

                // Tạo đơn hàng
                var order = new Order
                {
                    UserID = userIdInt,
                    OrderDate = DateTime.Now,
                    Status = "Chờ xử lý",
                    TotalAmount = finalAmount,
                    VoucherCode = voucherCode
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Chi tiết đơn hàng
                foreach (var item in cartItems)
                {
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price ?? item.Product?.Price ?? 0m
                    });
                }

                // Thanh toán
                _context.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentMethod = paymentMethod,
                    PaidAmount = finalAmount,
                    PaymentDate = DateTime.Now
                });

                // Xóa giỏ hàng
                _context.Carts.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // CAM KẾT GIAO DỊCH

                // GỬI THÔNG BÁO CHO ADMIN (SAU KHI COMMIT)
                var notification = new Notification
                {
                    UserID = null,  // Admin notification
                    Message = $"[ĐƠN MỚI] #{order.OrderID} - {user.FullName}",
                    Link = $"/Orders/Details/{order.OrderID}",
                    IsRead = false,  // ✅ LUÔN = false
                    OrderID = order.OrderID
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                // GỬI REAL-TIME CHO ADMIN
                await _hubContext.Clients.Group("Admins").SendAsync(
                    "ReceiveOrderNotification",
                    notification.Message,
                    notification.Link,
                    notification.NotificationId
                );

                TempData["SuccessMessage"] = $"Thanh toán thành công! Mã đơn hàng: <strong>#{order.OrderID}</strong>. " +
                                  (pointsUsed > 0 ? $"Bạn đã dùng {pointsUsed} điểm (trị giá {pointValue:N0}đ)." : "");

                return RedirectToAction("Index"); // Quay lại giỏ hàng
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> CheckoutOnline(string voucherCode = null, bool usePoints = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");
            int userIdInt = int.Parse(userId);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userIdInt);
            if (user == null) return RedirectToAction("Index", "Login");

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            // 🔹 Tính tổng tiền
            var totalAmount = cartItems.Sum(i => i.Product?.Price * i.Quantity ?? 0m);
            decimal discountAmount = 0m;

            // 🔹 Áp dụng voucher nếu có
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var voucher = await _context.Vouchers
                    .FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now);

                if (voucher != null)
                {
                    discountAmount = voucher.IsPercentage
                        ? totalAmount * voucher.DiscountAmount / 100m
                        : Math.Min(voucher.DiscountAmount, totalAmount);
                }
                else
                {
                    TempData["VoucherError"] = "Mã voucher không hợp lệ hoặc đã hết hạn.";
                    return RedirectToAction("Index");
                }
            }

            var amountAfterDiscount = totalAmount - discountAmount;
            if (amountAfterDiscount < 0) amountAfterDiscount = 0;

            // 🔹 Trừ điểm nếu có chọn dùng
            int pointsUsed = 0;
            decimal pointValue = 0;
            if (usePoints && user.Points > 0)
            {
                const decimal pointRate = 1000m; // 1 điểm = 1000 VNĐ
                pointValue = user.Points * pointRate;

                if (pointValue > amountAfterDiscount)
                {
                    pointsUsed = (int)(amountAfterDiscount / pointRate);
                    pointValue = pointsUsed * pointRate;
                }
                else
                {
                    pointsUsed = user.Points;
                }

                amountAfterDiscount -= pointValue;
                user.Points -= pointsUsed;
            }

            // 🔹 Tạo order trạng thái Pending
            var order = new Order
            {
                UserID = userIdInt,
                OrderDate = DateTime.Now,
                Status = "Chờ xử lý",
                TotalAmount = amountAfterDiscount,
                VoucherCode = voucherCode,
                OrderDetails = cartItems.Select(c => new OrderDetail
                {
                    ProductID = c.ProductID,
                    Quantity = c.Quantity,
                    UnitPrice = c.Product.Price
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 🔹 Tạo Payment trạng thái Pending
            var payment = new Payment
            {
                OrderID = order.OrderID,
                PaymentMethod = "VNPAY",
                PaidAmount = amountAfterDiscount,
                PaymentDate = DateTime.Now,
            };
            _context.Payments.Add(payment);

            // 🔹 Tạo notification cho Admin
            var notification = new Notification
            {
                Message = $"Khách hàng {user.FullName} vừa tạo đơn hàng #{order.OrderID}, chờ thanh toán online",
                Link = $"/Orders/Details/{order.OrderID}", // XÓA /Admin/                Type = "PendingOrder",
                CreatedAt = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            };
            _context.Notifications.Add(notification);

            // 🔹 Xóa giỏ hàng
            _context.Carts.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            // 🔹 Gửi real-time cho admin
            await _hubContext.Clients.Group("Admins").SendAsync(
               "ReceiveOrderNotification",
               notification.Message,
               notification.Link,      // ✅ "/Orders/Details/{order.OrderID}"
               notification.NotificationId  // ✅ ID từ DB
           );

            // 🔹 Redirect sang VNPAY
            return RedirectToAction(
                "CreatePayment",
                "Payment",
                new { orderId = order.OrderID, amount = amountAfterDiscount, voucherCode }
            );
        }


        [HttpPost]
        public async Task<IActionResult> Remove(int cartId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");
            int userIdInt = int.Parse(userId);

            var cartItem = await _context.Carts
                .Include(c => c.Product)
                .ThenInclude(p => p.BonusProduct)
                .FirstOrDefaultAsync(c => c.CartID == cartId && c.UserID == userIdInt);

            if (cartItem != null)
            {
                _context.Carts.Remove(cartItem);

                if (cartItem.Product?.BonusProduct != null)
                {
                    var bonusProductId = cartItem.Product.BonusProduct.ProductID;
                    var bonusCartItems = await _context.Carts
                        .Where(c => c.UserID == userIdInt && c.ProductID == bonusProductId)
                        .ToListAsync();

                    if (bonusCartItems.Any())
                    {
                        _context.Carts.RemoveRange(bonusCartItems);
                    }
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveVoucher()
        {
            TempData.Remove("VoucherCode");
            ViewBag.DiscountAmount = 0m; // Reset discount
            ViewBag.VoucherCode = null;  // Reset voucher code
            TempData["VoucherMessage"] = "Đã bỏ mã voucher.";
            return RedirectToAction("Index");
        }
    }
  
}