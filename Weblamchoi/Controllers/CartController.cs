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

            TempData["VoucherCode"] = voucherCode;
            TempData["DiscountAmount"] = discount;

            TempData["VoucherMessage"] = $"Áp dụng mã giảm giá thành công: {voucherCode}";
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> IndexAsync(string voucherCode = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");
            int userIdInt = int.Parse(userId);

            var cartItems = _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToList();

            decimal totalAmount = cartItems.Sum(c => (c.Price ?? c.Product?.Price ?? 0) * c.Quantity);

            if (string.IsNullOrEmpty(voucherCode))
            {
                voucherCode = TempData["VoucherCode"] as string;
                TempData.Keep("VoucherCode");
            }

            decimal discount = 0;
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var voucher = _context.Vouchers
                    .FirstOrDefault(v => v.Code == voucherCode && v.IsActive && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now);
                if (voucher != null)
                {
                    discount = voucher.IsPercentage
                        ? totalAmount * (voucher.DiscountAmount / 100m)
                        : Math.Min(voucher.DiscountAmount, totalAmount);
                }
            }

            decimal finalAmount = totalAmount - discount;
            if (finalAmount < 0) finalAmount = 0;
            ViewBag.Categories = await _context.Categories.ToListAsync();

            ViewBag.TotalAmount = totalAmount;
            ViewBag.DiscountAmount = discount;
            ViewBag.TotalAfterDiscount = finalAmount;
            ViewData["AppliedVoucherCode"] = voucherCode;

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
        public async Task<IActionResult> Checkout(string voucherCode, string paymentMethod)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Login", "Users");
            int userIdInt = int.Parse(userId);

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            decimal totalAmount = cartItems.Sum(item => item.Price.HasValue ? item.Price.Value * item.Quantity : (item.Product?.Price ?? 0m) * item.Quantity);
            decimal discountAmount = 0;

            Voucher voucher = null;
            if (!string.IsNullOrEmpty(voucherCode))
            {
                voucher = await _context.Vouchers
                    .Where(v => v.Code == voucherCode && v.IsActive && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now)
                    .FirstOrDefaultAsync();

                if (voucher != null)
                {
                    discountAmount = voucher.IsPercentage
                        ? totalAmount * (voucher.DiscountAmount / 100m)
                        : Math.Min(voucher.DiscountAmount, totalAmount);
                }
                else
                {
                    TempData["VoucherMessage"] = "Mã voucher không hợp lệ hoặc đã hết hạn.";
                    return RedirectToAction("Index");
                }
            }

            var finalAmount = totalAmount - discountAmount;

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

            foreach (var item in cartItems)
            {
                var detail = new OrderDetail
                {
                    OrderID = order.OrderID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    UnitPrice = item.Price.HasValue ? item.Price.Value : (item.Product?.Price ?? 0m)
                };
                _context.OrderDetails.Add(detail);
            }

            var payment = new Payment
            {
                OrderID = order.OrderID,
                PaymentMethod = paymentMethod,
                PaidAmount = finalAmount,
                PaymentDate = DateTime.Now
            };
            _context.Payments.Add(payment);

            _context.Carts.RemoveRange(cartItems);

            var notification = new Notification
            {
                Message = $"Khách hàng {order.User?.FullName ?? "Ẩn danh"} vừa đặt đơn hàng #{order.OrderID}",
                Link = $"/Orders/Details/{order.OrderID}",
                CreatedAt = DateTime.Now,
                IsRead = false,
                Type = "Order",
                OrderID = order.OrderID
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Gửi realtime cho admin qua SignalR
            await _hubContext.Clients.Group("Admins").SendAsync(
                "ReceiveOrderNotification",
                notification.Message,
                notification.OrderID
            );


            await _hubContext.Clients.User(userIdInt.ToString()).SendAsync("ReceiveCheckoutSuccess", "Thanh toán thành công!");

            TempData["Message"] = "Thanh toán thành công! Đơn hàng đã được lưu.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> CheckoutOnline(string voucherCode = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return RedirectToAction("Index", "Login");
            int userIdInt = int.Parse(userId);

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            var totalAmount = cartItems.Sum(i => i.Product?.Price * i.Quantity ?? 0m);
            decimal discountAmount = 0m;

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

            // 🔹 Tạo order ở trạng thái "Pending"
            var order = new Order
            {
                UserID = userIdInt,
                OrderDate = DateTime.Now,
                Status = "Pending",
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

            // 🔹 Tạo notification gắn với OrderID
            var notification = new Notification
            {
                Message = $"Khách hàng {userIdInt} vừa tạo đơn hàng #{order.OrderID}, đang chờ thanh toán online",
                Link = $"/Orders/Details/{order.OrderID}",
                Type = "PendingOrder",
                CreatedAt = DateTime.Now,
                IsRead = false,
                OrderID = order.OrderID
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // 🔹 Gửi real-time cho admin
            await _hubContext.Clients.Group("Admins").SendAsync(
                "ReceiveOrderNotification",
                notification.Message,
                order.OrderID
            );

            // 🔹 Chuyển sang tạo thanh toán VNPAY
            return RedirectToAction("CreatePayment", "Payment", new { orderId = order.OrderID, amount = amountAfterDiscount, voucherCode });
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
            TempData["VoucherMessage"] = "Đã bỏ mã voucher.";
            return RedirectToAction("Index");
        }
    }
  
}