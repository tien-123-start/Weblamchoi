using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using weblamchoi.Hubs;
using weblamchoi.Models;

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

        // === ÁP DỤNG VOUCHER ===
        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string voucherCode)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || !int.TryParse(userId, out int userIdInt))
                return RedirectToAction("Index", "Login");

            // Chỉ query 1 lần duy nhất
            var voucher = await _context.Vouchers
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Code == voucherCode);

            // Kiểm tra tồn tại trước
            if (voucher == null)
            {
                TempData["VoucherError"] = "Mã giảm giá không tồn tại.";
                return RedirectToAction("Index");
            }

            // Kiểm tra các điều kiện hợp lệ
            if (!voucher.IsActive)
            {
                TempData["VoucherError"] = "Mã giảm giá đã bị khóa.";
                return RedirectToAction("Index");
            }

            if (voucher.StartDate > DateTime.Now)
            {
                TempData["VoucherError"] = $"Mã chỉ áp dụng từ ngày {voucher.StartDate:dd/MM/yyyy}.";
                return RedirectToAction("Index");
            }

            if (voucher.EndDate < DateTime.Now)
            {
                TempData["VoucherError"] = $"Mã đã hết hạn ngày {voucher.EndDate:dd/MM/yyyy}.";
                return RedirectToAction("Index");
            }

            // Lấy giỏ hàng
            var cartItems = await _context.Carts
                .AsNoTracking()
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["VoucherError"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            // Tính tổng tiền
            decimal totalAmount = cartItems.Sum(c => (c.Price ?? c.Product?.Price ?? 0) * c.Quantity);

            // Tính giảm giá
            decimal discount = voucher.IsPercentage
                ? totalAmount * ((decimal)voucher.DiscountAmount / 100)
                : Math.Min(voucher.DiscountAmount, totalAmount);

            // Lưu TempData để hiển thị
            TempData["VoucherCode"] = voucherCode;
            TempData["VoucherDiscount"] = discount.ToString("F2");
            TempData["VoucherIsPercent"] = voucher.IsPercentage;
            TempData["VoucherValue"] = voucher.DiscountAmount.ToString("F2");
            TempData["VoucherMessage"] = voucher.IsPercentage
                ? $"Áp dụng mã: {voucherCode} (Giảm {voucher.DiscountAmount}%)"
                : $"Áp dụng mã: {voucherCode} (Giảm {voucher.DiscountAmount:N0}₫)";

            // Giữ lại TempData
            TempData.Keep("VoucherCode");
            TempData.Keep("VoucherDiscount");
            TempData.Keep("VoucherIsPercent");
            TempData.Keep("VoucherValue");

            return RedirectToAction("Index");
        }



        // === HIỂN THỊ GIỎ HÀNG ===
        public async Task<IActionResult> Index(string voucherCode = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || !int.TryParse(userId, out int userIdInt))
                return RedirectToAction("Index", "Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userIdInt);
            ViewBag.UserPoints = user?.Points ?? 0;

            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userIdInt)
                .ToListAsync();

            decimal totalAmount = cartItems.Sum(c => (c.Price ?? c.Product?.Price ?? 0) * c.Quantity);

            // Lấy voucher từ TempData hoặc DB
            if (string.IsNullOrEmpty(voucherCode))
                voucherCode = TempData["VoucherCode"] as string;

            decimal discount = 0;
            if (!string.IsNullOrEmpty(voucherCode))
            {
                var savedDiscount = TempData["VoucherDiscount"] as string;
                if (!string.IsNullOrEmpty(savedDiscount) && decimal.TryParse(savedDiscount, out decimal d))
                {
                    discount = d;
                }
                else
                {
                    var voucher = await _context.Vouchers
                        .FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now);
                    if (voucher != null)
                    {
                        discount = voucher.IsPercentage
                            ? totalAmount * (voucher.DiscountAmount / 100m)
                            : Math.Min(voucher.DiscountAmount, totalAmount);
                    }
                }
                TempData.Keep("VoucherCode");
                TempData.Keep("VoucherDiscount");
            }

            ViewBag.TotalAmount = totalAmount;
            ViewBag.DiscountAmount = discount;
            ViewBag.TotalAfterDiscount = Math.Max(totalAmount - discount, 0);
            ViewData["AppliedVoucherCode"] = voucherCode;
            ViewBag.Categories = await _context.Categories.ToListAsync();

            return View(cartItems);
        }

        // === THÊM SẢN PHẨM ===
        [HttpPost]
        public async Task<IActionResult> Add(int productId, int quantity = 1, decimal price = 0, int? bonusProductId = null, decimal bonusPrice = 0, bool applyDiscount = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || !int.TryParse(userId, out int userIdInt))
            {
                TempData["CartError"] = "Vui lòng đăng nhập.";
                return RedirectToAction("Index", "Login");
            }

            var product = await _context.Products.Include(p => p.BonusProduct)
                .FirstOrDefaultAsync(p => p.ProductID == productId);
            if (product == null)
            {
                TempData["CartError"] = "Sản phẩm không tồn tại.";
                return RedirectToAction("Index");
            }

            decimal productPrice = applyDiscount ? price : product.Price;

            var existing = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserID == userIdInt && c.ProductID == productId);

            if (existing != null)
            {
                existing.Quantity += quantity;
                existing.Price = productPrice;
                _context.Carts.Update(existing);
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

            // Thêm sản phẩm khuyến mãi
            if (applyDiscount && bonusProductId.HasValue)
            {
                var bonus = await _context.Products.FindAsync(bonusProductId.Value);
                if (bonus != null)
                {
                    var bonusExist = await _context.Carts
                        .FirstOrDefaultAsync(c => c.UserID == userIdInt && c.ProductID == bonusProductId.Value);

                    if (bonusExist != null)
                    {
                        bonusExist.Quantity += quantity;
                        bonusExist.Price = bonusPrice;
                        _context.Carts.Update(bonusExist);
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
            await _hubContext.Clients.User(userIdInt.ToString()).SendAsync("ReceiveCartUpdate", "Đã thêm vào giỏ.");
            TempData["SuccessMessage"] = "Đã thêm sản phẩm vào giỏ hàng.";
            return RedirectToAction("Details", "Products", new { id = productId });
        }

        // === CẬP NHẬT SỐ LƯỢNG ===
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int cartId, string action)
        {
            var item = await _context.Carts.FindAsync(cartId);
            if (item == null) return NotFound();

            if (action == "increase") item.Quantity++;
            else if (action == "decrease" && item.Quantity > 1) item.Quantity--;

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // === XÓA SẢN PHẨM ===
        [HttpPost]
        public async Task<IActionResult> Remove(int cartId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || !int.TryParse(userId, out int userIdInt))
                return RedirectToAction("Index", "Login");

            var item = await _context.Carts
                .Include(c => c.Product).ThenInclude(p => p.BonusProduct)
                .FirstOrDefaultAsync(c => c.CartID == cartId && c.UserID == userIdInt);

            if (item != null)
            {
                _context.Carts.Remove(item);

                if (item.Product?.BonusProduct != null)
                {
                    var bonusId = item.Product.BonusProduct.ProductID;
                    var bonusItems = await _context.Carts
                        .Where(c => c.UserID == userIdInt && c.ProductID == bonusId)
                        .ToListAsync();
                    _context.Carts.RemoveRange(bonusItems);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // === BỎ VOUCHER ===
        [HttpPost]
        public IActionResult RemoveVoucher()
        {
            TempData.Remove("VoucherCode");
            TempData.Remove("VoucherDiscount");
            TempData["VoucherMessage"] = "Đã bỏ mã voucher.";
            return RedirectToAction("Index");
        }

        // === CHECKOUT COD ===
        [HttpPost]
        public async Task<IActionResult> Checkout(
        string voucherCode,
        string paymentMethod,
        bool usePoints = false,
        decimal? shippingLat = null,
        decimal? shippingLng = null,
        string shippingAddress = null,
        decimal shippingFee = 0) // NHẬN TỪ FORM
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || !int.TryParse(userId, out int userIdInt))
                return RedirectToAction("Index", "Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userIdInt);
            var cartItems = await _context.Carts.Include(c => c.Product).Where(c => c.UserID == userIdInt).ToListAsync();
            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng trống.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalAmount = cartItems.Sum(i => (i.Price ?? i.Product?.Price ?? 0) * i.Quantity);
                decimal discount = await CalculateDiscount(voucherCode, totalAmount);
                decimal amountAfterDiscount = totalAmount - discount;

                // DÙNG PHÍ SHIP TỪ FORM (frontend đã tính)
                decimal subtotal = amountAfterDiscount + shippingFee;

                int pointsUsed = 0;
                if (usePoints && user.Points > 0)
                {
                    decimal pointValue = Math.Min(user.Points * 1000m, subtotal);
                    pointsUsed = (int)(pointValue / 1000m);
                    subtotal -= pointValue;
                    user.Points -= pointsUsed;
                    _context.Users.Update(user);
                }

                var order = new Order
                {
                    UserID = userIdInt,
                    OrderDate = DateTime.Now,
                    Status = "Chờ xử lý",
                    TotalAmount = subtotal,
                    VoucherCode = voucherCode
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cartItems)
                {
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price ?? item.Product?.Price ?? 0
                    });
                }

                if (shippingLat.HasValue && shippingLng.HasValue && !string.IsNullOrEmpty(shippingAddress))
                {
                    _context.Shippings.Add(new Shipping
                    {
                        OrderID = order.OrderID,
                        ShippingAddress = shippingAddress,
                        ShippingMethod = "Giao hàng tiêu chuẩn",
                        ShippingFee = shippingFee,
                        DestinationLat = shippingLat.ToString(),
                        DestinationLng = shippingLng.ToString()
                    });
                }

                _context.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentMethod = paymentMethod,
                    PaidAmount = subtotal,
                    PaymentDate = DateTime.Now,
                    Status = "Completed"
                });

                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                await SendAdminNotification(order, user, "COD");

                TempData["SuccessMessage"] = $"Đặt hàng thành công! Mã: #{order.OrderID}. " +
                    (pointsUsed > 0 ? $"Dùng {pointsUsed} điểm. " : "") +
                    (shippingFee > 0 ? $"Phí ship: {shippingFee:N0}₫." : "Miễn phí ship.");

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        private async Task<decimal> CalculateDiscount(string voucherCode, decimal totalAmount)
        {
            if (string.IsNullOrEmpty(voucherCode))
                return 0m;

            // Giả sử bạn có DbContext tên _context
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == voucherCode && v.IsActive);

            if (voucher == null)
                return 0m;

            decimal discount = 0;

            if (voucher.Type == "Percentage") // giảm theo %
            {
                discount = totalAmount * voucher.Value / 100m;
            }
            else if (voucher.Type == "Fixed") // giảm theo tiền cố định
            {
                discount = voucher.Value;
            }

            // Giới hạn discount không vượt quá totalAmount
            return Math.Min(discount, totalAmount);
        }


        [HttpPost]
        public async Task<IActionResult> CheckoutOnline(
            string voucherCode = null,
            bool usePoints = false,
            decimal? shippingLat = null,
            decimal? shippingLng = null,
            string shippingAddress = null,
            decimal shippingFee = 0) // NHẬN TỪ FORM
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null || !int.TryParse(userId, out int userIdInt))
                return RedirectToAction("Index", "Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userIdInt);
            var cartItems = await _context.Carts.Include(c => c.Product).Where(c => c.UserID == userIdInt).ToListAsync();
            if (!cartItems.Any())
            {
                TempData["Message"] = "Giỏ hàng trống.";
                return RedirectToAction("Index");
            }

            if (!shippingLat.HasValue || !shippingLng.HasValue || string.IsNullOrEmpty(shippingAddress))
            {
                TempData["ShippingError"] = "Vui lòng chọn địa chỉ giao hàng.";
                return RedirectToAction("Index");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal totalAmount = cartItems.Sum(i => (i.Price ?? i.Product?.Price ?? 0) * i.Quantity);
                decimal discount = await CalculateDiscount(voucherCode, totalAmount);
                decimal amountAfterDiscount = totalAmount - discount;

                // DÙNG PHÍ SHIP TỪ FORM
                decimal subtotal = amountAfterDiscount + shippingFee;

                int pointsUsed = 0;
                if (usePoints && user.Points > 0)
                {
                    decimal pointValue = Math.Min(user.Points * 1000m, subtotal);
                    pointsUsed = (int)(pointValue / 1000m);
                    subtotal -= pointValue;
                    user.Points -= pointsUsed;
                    _context.Users.Update(user);
                }

                var order = new Order
                {
                    UserID = userIdInt,
                    OrderDate = DateTime.Now,
                    Status = "Chờ thanh toán",
                    TotalAmount = subtotal,
                    VoucherCode = voucherCode
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cartItems)
                {
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price ?? item.Product?.Price ?? 0
                    });
                }

                _context.Shippings.Add(new Shipping
                {
                    OrderID = order.OrderID,
                    ShippingAddress = shippingAddress,
                    ShippingMethod = "Giao hàng tiêu chuẩn",
                    ShippingFee = shippingFee,
                    DestinationLat = shippingLat.ToString(),
                    DestinationLng = shippingLng.ToString()
                });

                _context.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentMethod = "VNPAY",
                    PaidAmount = subtotal,
                    PaymentDate = null,
                    Status = "Pending"
                });

                //_context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                await SendAdminNotification(order, user, "Chờ xử lý VNPAY");

                return RedirectToAction("CreatePayment", "Payment", new
                {
                    orderId = order.OrderID,
                    amount = subtotal,
                    voucherCode,
                    pointsUsed,
                    shippingFee
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi thanh toán: " + ex.Message;
                return RedirectToAction("Index");
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutMomo(
      string? voucherCode,
      bool usePoints = false,
      decimal? shippingLat = null,
      decimal? shippingLng = null,
      string? shippingAddress = null,
      decimal shippingFee = 0)
        {
            // === 1. XÁC THỰC USER ===
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return RedirectToAction("Index", "Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null)
                return RedirectToAction("Index", "Login");

            // === 2. KIỂM TRA GIỎ HÀNG ===
            var cartItems = await _context.Carts
                .Include(c => c.Product)
                .Where(c => c.UserID == userId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng trống.";
                return RedirectToAction("Index");
            }

            // === 3. KIỂM TRA ĐỊA CHỈ ===
            if (!shippingLat.HasValue || !shippingLng.HasValue || string.IsNullOrWhiteSpace(shippingAddress))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn địa chỉ giao hàng.";
                return RedirectToAction("Index");
            }

            // === 4. TRANSACTION ===
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // TÍNH TỔNG TIỀN
                decimal totalAmount = cartItems.Sum(i => (i.Product?.Price ?? 0m) * i.Quantity);
                decimal discount = await CalculateDiscount(voucherCode, totalAmount, userId);
                decimal amountAfterDiscount = totalAmount - discount;
                decimal subtotal = amountAfterDiscount + shippingFee;

                // DÙNG ĐIỂM
                int pointsUsed = 0;
                if (usePoints && user.Points > 0)
                {
                    decimal pointValue = Math.Min(user.Points * 1000m, subtotal);
                    subtotal -= pointValue;
                    pointsUsed = (int)(pointValue / 1000m);
                    user.Points -= pointsUsed;
                    _context.Users.Update(user);
                }

                // TẠO ĐƠN HÀNG
                var order = new Order
                {
                    UserID = userId,
                    OrderDate = DateTime.Now,
                    Status = "Chờ thanh toán",
                    TotalAmount = subtotal,
                    VoucherCode = voucherCode,
                    PointsUsed = pointsUsed
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // LẤY OrderID

                // CHI TIẾT ĐƠN HÀNG
                foreach (var item in cartItems)
                {
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Product?.Price ?? 0m,
                        Price = (item.Product?.Price ?? 0m) * item.Quantity
                    });
                }

                // GIAO HÀNG
                _context.Shippings.Add(new Shipping
                {
                    OrderID = order.OrderID,
                    ShippingAddress = shippingAddress,
                    ShippingMethod = "Giao hàng tiêu chuẩn",
                    ShippingFee = shippingFee,
                    DestinationLat = shippingLat.Value.ToString("F6"),
                    DestinationLng = shippingLng.Value.ToString("F6")
                });

                // THANH TOÁN
                _context.Payments.Add(new Payment
                {
                    OrderID = order.OrderID,
                    PaymentMethod = "MoMo",
                    Amount = subtotal,
                    PaidAmount = 0m,
                    Status = "Pending"
                });

                // GỌI TRANSACTION CHO MOMO
                var momoTransaction = new MomoTransaction
                {
                    OrderID = order.OrderID,
                    Amount = subtotal,
                    OrderInfo = $"Thanh toán đơn hàng #{order.OrderID}",
                    OrderType = "momo_wallet",
                    PayType = "web",
                    ResultCode = "0",
                    Message = "Chờ thanh toán"
                };
                _context.MomoTransactions.Add(momoTransaction);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // XÓA GIỎ HÀNG
                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // GỬI THÔNG BÁO ADMIN
                await SendAdminNotification(order, user, "Đơn hàng mới - Chờ xử lý MoMo");

                // === CHUYỂN HƯỚNG TỚI MOMO ===
                return RedirectToAction("Create", "Momo", new
                {
                    orderId = order.OrderID,
                    amount = (long)(subtotal * 1), // VNĐ → đồng (MoMo dùng đồng)
                    orderInfo = $"Thanh toán đơn hàng #{order.OrderID}",
                    extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(order.OrderID.ToString()))
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi xử lý đơn hàng: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
        // === TÍNH PHÍ SHIP (API) ===
        [HttpPost]
        public async Task<JsonResult> CalculateShipping([FromBody] ShippingDto dto)
        {
            if (dto == null || dto.lat == 0 || dto.lng == 0)
                return Json(new { success = false, message = "Thiếu tọa độ!" });

            double storeLat = 10.7769;  // Cập nhật tọa độ cửa hàng
            double storeLng = 106.7009;

            string osrmUrl = $"https://router.project-osrm.org/route/v1/driving/{storeLng},{storeLat};{dto.lng},{dto.lat}?overview=false";

            using var client = new HttpClient();
            try
            {
                var resp = await client.GetAsync(osrmUrl);
                if (!resp.IsSuccessStatusCode)
                    return Json(new { success = false, message = "Không kết nối được OSRM." });

                var json = await resp.Content.ReadAsStringAsync();
                var data = System.Text.Json.JsonSerializer.Deserialize<OsrmResponse>(json);

                if (data?.routes == null || !data.routes.Any())
                    return Json(new { success = false, message = "Không tìm được tuyến đường." });

                var distanceMeters = data.routes[0].distance;
                var distanceKm = Math.Round(distanceMeters / 1000, 1);
                var shippingFee = (int)Math.Ceiling(distanceKm * 8000); // 8k/km

                return Json(new
                {
                    success = true,
                    shippingFee,
                    distance = distanceKm  // ĐẢM BẢO TRẢ VỀ distance
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // === HÀM HỖ TRỢ ===
        private async Task<decimal> CalculateDiscount(string code, decimal total, int userId)
        {
            if (string.IsNullOrEmpty(code)) return 0;
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == code && v.IsActive && v.StartDate <= DateTime.Now && v.EndDate >= DateTime.Now);
            if (voucher == null) return 0;
            return voucher.IsPercentage ? total * (voucher.DiscountAmount / 100m) : Math.Min(voucher.DiscountAmount, total);
        }

        private async Task SendAdminNotification(Order order, User user, string type)
        {
            var msg = $"[ĐƠN {type}] #{order.OrderID} - {user.FullName}";
            var noti = new Notification
            {
                Message = msg,
                Link = $"/Orders/Details/{order.OrderID}",
                IsRead = false,
                OrderID = order.OrderID,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group("Admins").SendAsync("ReceiveOrderNotification", msg, noti.Link, noti.NotificationId);
        }

        private async Task<decimal> GetDistanceFromOSRM(string storeLat, string storeLng, decimal destLat, decimal destLng)
        {
            var client = new HttpClient();
            var url = $"http://router.project-osrm.org/route/v1/driving/{storeLng},{storeLat};{destLng},{destLat}?overview=false";
            try
            {
                var res = await client.GetAsync(url);
                if (!res.IsSuccessStatusCode) return -1;
                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("routes", out var routes) && routes.GetArrayLength() > 0)
                {
                    return (decimal)(routes[0].GetProperty("distance").GetDouble() / 1000);
                }
            }
            catch { }
            return -1;
        }
        public class ShippingDto
        {
            public double lat { get; set; }
            public double lng { get; set; }
            public string address { get; set; }
        }

        public class OsrmResponse
        {
            public List<OsrmRoute> routes { get; set; }
        }

        public class OsrmRoute
        {
            public double distance { get; set; }
        }

    }
}