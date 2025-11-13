@model List<weblamchoi.Models.Cart>
@using System.Globalization
@{
    var vi = new CultureInfo("vi-VN");
    var totalAmount = ViewBag.TotalAmount ?? 0m;
    var discountAmount = TempData["VoucherDiscount"] != null ? Convert.ToDecimal(TempData["VoucherDiscount"]) : 0;
    var totalAfterDiscount = ViewBag.TotalAfterDiscount ?? totalAmount;
    var userPoints = ViewBag.UserPoints ?? 0;
    var voucherCode = TempData["VoucherCode"]?.ToString();
    var voucherValue = TempData["VoucherValue"] != null ? Convert.ToDecimal(TempData["VoucherValue"]) : 0;
    var isPercent = TempData["VoucherIsPercent"] != null && (bool)TempData["VoucherIsPercent"];
    decimal discount = TempData["VoucherDiscount"] != null ? Convert.ToDecimal(TempData["VoucherDiscount"]) : 0;
    if (discount > 0) discountAmount = discount;

    var mainProducts = Model?.Where(c => c.Product?.BonusProduct != null).ToList() ?? new List<Cart>();
    var bonusProductIds = mainProducts.Select(m => m.Product.BonusProduct.ProductID).ToHashSet();
    var bonusProducts = Model?.Where(c => bonusProductIds.Contains(c.ProductID)).ToList() ?? new List<Cart>();
    var standaloneProducts = Model?.Except(mainProducts).Except(bonusProducts).ToList() ?? new List<Cart>();

    Func<decimal?, string> formatCurrency = (price) =>
        price.HasValue ? $"{price.Value:N0} ₫" : "0 ₫";
}
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <title>Giỏ hàng - Điện Máy Xanh</title>
    <link rel="stylesheet" href="/css/cart.css">
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <link rel="stylesheet" href="https://unpkg.com/leaflet-routing-machine@latest/dist/leaflet-routing-machine.css" />
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet">
    <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons/font/bootstrap-icons.css" rel="stylesheet">
</head>
<body>
    <div class="container py-4">
        <div class="page-header text-center mb-4"><h2>Giỏ hàng của bạn</h2></div>

        <!-- THÔNG BÁO -->
        @if (TempData["SuccessMessage"] != null)
        {
            <div class="alert alert-success alert-dismissible fade show text-center" role="alert">
                @Html.Raw(TempData["SuccessMessage"])
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        }
        @if (TempData["ErrorMessage"] != null)
        {
            <div class="alert alert-danger alert-dismissible fade show text-center" role="alert">
                @TempData["ErrorMessage"]
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        }

        <!-- GIỎ HÀNG -->
        <div class="cart-container">
            @if (!Model?.Any() ?? true)
            {
                <div class="empty-cart text-center py-5">
                    <i class="bi bi-cart-x" style="font-size:4rem;color:#ccc;"></i>
                    <h3 class="mt-3">Giỏ hàng trống</h3>
                    <a href="/Home/Index" class="btn btn-primary px-4">Tiếp tục mua sắm</a>
                </div>
            }
            else
            {
                <div class="card mb-4">
                    <div class="card-body p-0">
                        <table class="table modern-table mb-0">
                            <thead>
                                <tr>
                                    <th>Sản phẩm</th>
                                    <th>Giá</th>
                                    <th>Số lượng</th>
                                    <th>Thành tiền</th>
                                    <th>Xóa</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach (var mainItem in mainProducts)
                                {
                                    var bonusItem = bonusProducts.FirstOrDefault(b => b.ProductID == mainItem.Product.BonusProduct.ProductID);
                                    <tr class="align-middle">
                                        <td>
                                            <div class="product-info-vertical">
                                                <img src="@(mainItem.Product.ImageURL ?? "/images/default.png")" class="product-image" />
                                                <div>
                                                    <div class="product-name">@mainItem.Product.ProductName</div>
                                                    @if (mainItem.Price < mainItem.Product.Price)
                                                    {
                                                        <small class="text-success">Đã giảm giá</small>
                                                    }
                                                </div>
                                            </div>
                                        </td>
                                        <td>
                                            <del class="text-muted small">@formatCurrency(mainItem.Product.Price)</del><br />
                                            <strong class="text-danger">@formatCurrency(mainItem.Price ?? mainItem.Product.Price)</strong>
                                        </td>
                                        <td>
                                            <form method="post" asp-action="UpdateQuantity" class="quantity-controls d-inline">
                                                <input type="hidden" name="cartId" value="@mainItem.CartID" />
                                                <button type="submit" name="action" value="decrease" class="quantity-btn btn btn-outline-secondary" @(mainItem.Quantity <= 1 ? "disabled" : "")>-</button>
                                                <span class="quantity-display mx-2">@mainItem.Quantity</span>
                                                <button type="submit" name="action" value="increase" class="quantity-btn btn btn-outline-secondary">+</button>
                                            </form>
                                        </td>
                                        <td class="fw-bold text-end">@formatCurrency((mainItem.Price ?? mainItem.Product.Price) * mainItem.Quantity)</td>
                                        <td>
                                            <form asp-action="Remove" method="post" class="d-inline">
                                                <input type="hidden" name="cartId" value="@mainItem.CartID" />
                                                <button type="submit" class="remove-btn btn btn-link p-0" onclick="return confirm('Xóa sản phẩm?')">
                                                    <i class="bi bi-trash"></i>
                                                </button>
                                            </form>
                                        </td>
                                    </tr>
                                    @if (bonusItem != null)
                                    {
                                        <tr class="bonus-product-row bg-light align-middle">
                                            <td>
                                                <div class="product-info-vertical">
                                                    <img src="@(bonusItem.Product.ImageURL ?? "/images/default.png")" class="product-image" />
                                                    <div>
                                                        <div class="product-name">@bonusItem.Product.ProductName</div>
                                                        <span class="badge bg-success">Tặng kèm</span>
                                                    </div>
                                                </div>
                                            </td>
                                            <td><strong class="text-success">@formatCurrency(bonusItem.Price ?? bonusItem.Product.Price)</strong></td>
                                            <td><span class="quantity-display">@bonusItem.Quantity</span></td>
                                            <td class="text-success fw-bold text-end">@formatCurrency((bonusItem.Price ?? bonusItem.Product.Price) * bonusItem.Quantity)</td>
                                            <td></td>
                                        </tr>
                                    }
                                }
                                @foreach (var item in standaloneProducts)
                                {
                                    <tr class="align-middle">
                                        <td>
                                            <div class="product-info-vertical">
                                                <img src="@(item.Product.ImageURL ?? "/images/default.png")" class="product-image" />
                                                <div>
                                                    <div class="product-name">@item.Product.ProductName</div>
                                                    @if (item.Price < item.Product.Price)
                                                    {
                                                        <small class="text-success">Đã giảm giá</small>
                                                    }
                                                </div>
                                            </div>
                                        </td>
                                        <td>
                                            <del class="text-muted small">@formatCurrency(item.Product.Price)</del><br />
                                            <strong class="text-danger">@formatCurrency(item.Price ?? item.Product.Price)</strong>
                                        </td>
                                        <td>
                                            <form method="post" asp-action="UpdateQuantity" class="quantity-controls d-inline">
                                                <input type="hidden" name="cartId" value="@item.CartID" />
                                                <button type="submit" name="action" value="decrease" class="quantity-btn btn btn-outline-secondary" @(item.Quantity <= 1 ? "disabled" : "")>-</button>
                                                <span class="quantity-display mx-2">@item.Quantity</span>
                                                <button type="submit" name="action" value="increase" class="quantity-btn btn btn-outline-secondary">+</button>
                                            </form>
                                        </td>
                                        <td class="fw-bold text-end">@formatCurrency((item.Price ?? item.Product.Price) * item.Quantity)</td>
                                        <td>
                                            <form asp-action="Remove" method="post" class="d-inline">
                                                <input type="hidden" name="cartId" value="@item.CartID" />
                                                <button type="submit" class="remove-btn btn btn-link p-0" onclick="return confirm('Xóa sản phẩm?')">
                                                    <i class="bi bi-trash"></i>
                                                </button>
                                            </form>
                                        </td>
                                    </tr>
                                }
                            </tbody>
                        </table>
                    </div>
                </div>
            }
        </div>

        <!-- TÓM TẮT ĐƠN HÀNG -->
        <div class="summary-section card mb-4">
            <div class="card-header">Tóm tắt đơn hàng</div>
            <div class="card-body">
                <div class="summary-row"><span>Tổng tiền hàng</span><strong>@formatCurrency(totalAmount)</strong></div>
                @if (!string.IsNullOrEmpty(voucherCode))
                {
                    <div class="summary-row text-success">
                        <span>
                            Áp dụng Voucher: <strong>@voucherCode</strong>
                            (@(isPercent ? $"Giảm {voucherValue}%" : $"Giảm {voucherValue:N0} ₫"))
                        </span>
                        <strong>−@discountAmount:N0 ₫</strong>
                    </div>
                }
                <div class="summary-row" id="shippingRow" style="display:none;">
                    <span>Phí vận chuyển</span><strong id="shippingFee">0 ₫</strong>
                </div>
                <div class="summary-row">
                    <label class="form-check">
                        <input type="checkbox" id="usePointsCheckbox" class="form-check-input" />
                        <span class="form-check-label">Sử dụng <span id="userPoints">@userPoints</span> điểm <small class="text-muted">(1 điểm = 1.000₫)</small></span>
                    </label>
                </div>
                <div class="summary-row total mt-3">
                    <span><strong>Tổng thanh toán</strong></span>
                    <strong id="finalTotal" class="text-primary fs-5">@formatCurrency(totalAfterDiscount)</strong>
                </div>
            </div>
        </div>

        <!-- ĐỊA CHỈ GIAO HÀNG -->
        <div class="card mb-4">
            <div class="card-header">Địa chỉ giao hàng</div>
            <div class="card-body">
                <div class="mb-2">
                    <button type="button" class="btn btn-outline-primary btn-sm" onclick="getCurrentLocation()">Lấy vị trí hiện tại</button>
                    <small class="text-muted d-block">Nếu bị chặn: Click khóa → Site settings → Location → Allow → Reload</small>
                </div>
                <div class="input-group mb-2">
                    <input type="text" id="shippingAddress" class="form-control" placeholder="VD: 123 Lê Lợi, Quận 1, TP.HCM" />
                    <button type="button" class="btn btn-outline-secondary" onclick="geocodeAddress()">Tìm tọa độ</button>
                </div>
                <button type="button" class="btn btn-success w-100" onclick="calculateShippingFee()">Tính phí vận chuyển</button>
                <div id="shippingResult" class="mt-3"></div>
                <input type="hidden" id="shippingLat" /><input type="hidden" id="shippingLng" />
                <div id="map" style="height:300px;"></div>
            </div>
        </div>

        <!-- MÃ GIẢM GIÁ -->
        <div class="voucher-section card mb-4">
            <div class="card-header">Mã giảm giá</div>
            <div class="card-body">
                <form method="post" asp-action="ApplyVoucher" class="d-flex gap-2">
                    <input type="text" name="voucherCode" class="form-control" placeholder="Nhập mã"
                           value="@voucherCode" @(string.IsNullOrEmpty(voucherCode) ? "" : "readonly") />
                    <button type="submit" class="btn btn-primary" @(string.IsNullOrEmpty(voucherCode) ? "" : "disabled")>Áp dụng</button>
                </form>
                @if (!string.IsNullOrEmpty(voucherCode))
                {
                    <div class="mt-2 d-flex justify-content-between align-items-center">
                        <span>Đã áp dụng: <strong>@voucherCode</strong></span>
                        <form asp-action="RemoveVoucher" method="post" class="d-inline">
                            <button type="submit" class="btn btn-sm btn-outline-danger">Bỏ</button>
                        </form>
                    </div>
                }
            </div>
        </div>

        <!-- THANH TOÁN -->
        <div class="payment-section card">
            <div class="card-header">Chọn phương thức thanh toán</div>
            <div class="card-body">
                <div class="row g-3">
                    <!-- ONLINE -->
                    <div class="col-md-6">
                        <div class="payment-card d-flex flex-column h-100">
                            <h5>Thanh toán Online</h5>
                            <p class="text-muted mb-3">Chọn VNPAY hoặc MoMo</p>
                            <div class="input-group mb-3">
                                <label class="input-group-text"><i class="bi bi-credit-card"></i></label>
                                <select id="onlinePaymentMethod" class="form-select">
                                    <option value="vnpay" selected>VNPAY</option>
                                    <option value="momo">MoMo</option>
                                </select>
                            </div>

                            <!-- VNPAY FORM -->
                            <div id="vnpayFormContainer">
                                <form id="vnpayForm" method="post" action="/Payment/CreateVNPAY">
                                    <input type="hidden" name="voucherCode" value="@voucherCode" />
                                    <input type="hidden" name="usePoints" id="vnpayUsePoints" />
                                    <input type="hidden" name="shippingLat" id="vnpayShippingLat" />
                                    <input type="hidden" name="shippingLng" id="vnpayShippingLng" />
                                    <input type="hidden" name="shippingAddress" id="vnpayShippingAddress" />
                                    <input type="hidden" name="shippingFee" id="vnpayShippingFee" />
                                    <button type="submit" id="vnpaySubmitBtn" class="btn btn-success w-100 mt-auto">
                                        Thanh toán VNPAY
                                    </button>
                                </form>
                            </div>

                            <!-- MOMO FORM -->
                            <div id="momoFormContainer" style="display:none;">
                                <form id="momoForm" method="get" action="/Momo/Create">
                                    <input type="hidden" name="orderId" value="@ViewBag.OrderId" />
                                    <input type="hidden" name="orderInfo" value="Thanh toán đơn hàng qua MoMo" />
                                    <input type="hidden" name="amount" id="momoAmount" />
                                    <input type="hidden" name="voucherCode" value="@voucherCode" />
                                    <input type="hidden" name="usePoints" id="momoUsePoints" />
                                    <input type="hidden" name="shippingLat" id="momoShippingLat" />
                                    <input type="hidden" name="shippingLng" id="momoShippingLng" />
                                    <input type="hidden" name="shippingAddress" id="momoShippingAddress" />
                                    <input type="hidden" name="shippingFee" id="momoShippingFee" />
                                    <button type="submit" id="momoSubmitBtn" class="btn btn-danger w-100 mt-auto">
                                        <img src="https://momo.vn/images/logo-momo.png" width="18" class="me-1" style="vertical-align:text-bottom">
                                        Thanh toán MoMo QR
                                    </button>
                                </form>
                            </div>
                        </div>
                    </div>

                    <!-- TẠI CỬA HÀNG -->
                    <div class="col-md-6">
                        <div class="payment-card d-flex flex-column h-100">
                            <h5>Thanh toán tại cửa hàng</h5>
                            <p class="text-muted mb-3">Nhận hàng & thanh toán trực tiếp</p>
                            <form method="post" asp-action="Checkout" class="mt-auto">
                                <input type="hidden" name="paymentMethod" value="Tại cửa hàng" />
                                <input type="hidden" name="voucherCode" value="@voucherCode" />
                                <input type="hidden" name="usePoints" id="codUsePoints" />
                                <input type="hidden" name="shippingLat" id="codShippingLat" />
                                <input type="hidden" name="shippingLng" id="codShippingLng" />
                                <input type="hidden" name="shippingAddress" id="codShippingAddress" />
                                <input type="hidden" name="shippingFee" id="codShippingFee" />
                                <button type="submit" class="btn btn-primary w-100">Xác nhận đơn</button>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- JS -->
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script src="https://unpkg.com/leaflet-routing-machine@latest/dist/leaflet-routing-machine.js"></script>
    <script>
        let shippingFee = 0;
        const totalAmount = @totalAmount;
        const discountAmount = @discountAmount;
        const userPoints = @userPoints;
        const vi = new Intl.NumberFormat('vi-VN');
        const orderId = @ViewBag.OrderId;

        let map, userMarker, storeMarker, routeControl;
        const STORE_LAT = 10.7769, STORE_LNG = 106.7009;

        function initMap() {
            map = L.map('map').setView([STORE_LAT, STORE_LNG], 13);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
            storeMarker = L.marker([STORE_LAT, STORE_LNG]).addTo(map).bindPopup('Cửa hàng').openPopup();
        }

        function setShippingCoords(lat, lng, address) {
            document.getElementById('shippingLat').value = lat;
            document.getElementById('shippingLng').value = lng;
            ['vnpay', 'momo', 'cod'].forEach(p => {
                const prefix = p === 'vnpay' ? 'vnpay' : p;
                document.getElementById(`${prefix}ShippingLat`).value = lat;
                document.getElementById(`${prefix}ShippingLng`).value = lng;
                document.getElementById(`${prefix}ShippingAddress`).value = address;
            });
            const pos = [lat, lng];
            if (!userMarker) {
                userMarker = L.marker(pos).addTo(map).bindPopup('Bạn đang ở đây');
            } else {
                userMarker.setLatLng(pos);
            }
            map.setView(pos, 14);
            if (routeControl) map.removeControl(routeControl);
            routeControl = L.Routing.control({
                waypoints: [L.latLng(STORE_LAT, STORE_LNG), L.latLng(lat, lng)],
                routeWhileDragging: false,
                addWaypoints: false,
                createMarker: () => null,
                lineOptions: { styles: [{ color: '#0d6efd', weight: 5 }] },
                router: L.Routing.osrmv1({ serviceUrl: 'https://router.project-osrm.org/route/v1' })
            }).addTo(map);
            saveShippingData(lat, lng, address);
        }

        function saveShippingData(lat, lng, address, fee = null) {
            const data = { lat, lng, address };
            if (fee !== null) data.shippingFee = fee;
            localStorage.setItem('cartShipping', JSON.stringify(data));
        }

        function loadSavedShipping() {
            const saved = localStorage.getItem('cartShipping');
            if (!saved) return;
            try {
                const data = JSON.parse(saved);
                if (data.lat && data.lng && data.address) {
                    document.getElementById('shippingAddress').value = data.address;
                    document.getElementById('shippingLat').value = data.lat;
                    document.getElementById('shippingLng').value = data.lng;
                    setShippingCoords(data.lat, data.lng, data.address);
                    if (data.shippingFee) {
                        shippingFee = data.shippingFee;
                        document.getElementById('shippingRow').style.display = 'flex';
                        document.getElementById('shippingResult').innerHTML = `
                            <div class="alert alert-success">
                                <strong>Địa chỉ:</strong> ${data.address}<br>
                                <strong>Khoảng cách:</strong> Đã lưu<br>
                                <strong>Phí ship:</strong> ${vi.format(data.shippingFee)} ₫
                            </div>`;
                        syncPaymentData();
                    }
                }
            } catch { localStorage.removeItem('cartShipping'); }
        }

        function syncPaymentData() {
            const use = document.getElementById('usePointsCheckbox').checked;
            const subtotal = totalAmount - discountAmount + shippingFee;
            const pointValue = use ? Math.min(userPoints * 1000, subtotal) : 0;
            const final = Math.max(subtotal - pointValue, 0);

            // Cập nhật UI
            document.getElementById('finalTotal').textContent = vi.format(final) + ' ₫';
            document.getElementById('shippingFee').textContent = vi.format(shippingFee) + ' ₫';

            // Đồng bộ hidden fields
            ['vnpay', 'momo', 'cod'].forEach(p => {
                const prefix = p === 'vnpay' ? 'vnpay' : p;
                const useEl = document.getElementById(`${prefix}UsePoints`);
                const latEl = document.getElementById(`${prefix}ShippingLat`);
                const lngEl = document.getElementById(`${prefix}ShippingLng`);
                const addrEl = document.getElementById(`${prefix}ShippingAddress`);
                const feeEl = document.getElementById(`${prefix}ShippingFee`);
                if (useEl) useEl.value = use;
                if (latEl) latEl.value = document.getElementById('shippingLat').value;
                if (lngEl) lngEl.value = document.getElementById('shippingLng').value;
                if (addrEl) addrEl.value = document.getElementById('shippingAddress').value;
                if (feeEl) feeEl.value = shippingFee;
            });

            // CẬP NHẬT MOMO AMOUNT + ACTION
            const momoAmount = document.getElementById('momoAmount');
            if (momoAmount) momoAmount.value = final;

            const momoForm = document.getElementById('momoForm');
            if (momoForm) {
                momoForm.action = `/Momo/Create?orderId=${orderId}&amount=${final}&orderInfo=${encodeURIComponent('Thanh toán đơn hàng qua MoMo')}`;
            }
        }

        async function getCurrentLocation() {
            if (!navigator.geolocation) return alert('Trình duyệt không hỗ trợ định vị.');
            navigator.geolocation.getCurrentPosition(async pos => {
                await reverseGeocode(pos.coords.latitude, pos.coords.longitude);
                calculateShippingFee();
            }, err => {
                alert(err.code === 1 ? 'Quyền vị trí bị chặn. Vui lòng cho phép.' : 'Lỗi: ' + err.message);
            });
        }

        async function geocodeAddress() {
            const addr = document.getElementById('shippingAddress').value.trim();
            if (!addr) return alert('Nhập địa chỉ!');
            const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(addr)}&countrycodes=vn&limit=1`;
            try {
                const r = await fetch(url, {headers:{'User-Agent':'WeblamchoiApp/1.0'}});
                const d = await r.json();
                if (d?.length) {
                    const {lat, lon, display_name} = d[0];
                    document.getElementById('shippingAddress').value = display_name;
                    setShippingCoords(lat, lon, display_name);
                    calculateShippingFee();
                } else alert('Không tìm thấy địa chỉ.');
            } catch { alert('Lỗi kết nối.'); }
        }

        async function reverseGeocode(lat, lng) {
            const url = `https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&zoom=18`;
            try {
                const r = await fetch(url, {headers:{'User-Agent':'WeblamchoiApp/1.0'}});
                const d = await r.json();
                if (d?.display_name) {
                    document.getElementById('shippingAddress').value = d.display_name;
                    setShippingCoords(lat, lng, d.display_name);
                }
            } catch {}
        }

        async function calculateShippingFee() {
            const lat = document.getElementById('shippingLat').value;
            const lng = document.getElementById('shippingLng').value;
            const addr = document.getElementById('shippingAddress').value;
            if (!lat || !lng || !addr) return alert('Chọn địa chỉ trước!');
            const res = await fetch('/Cart/CalculateShipping', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ lat: parseFloat(lat), lng: parseFloat(lng), address: addr })
            });
            const data = await res.json();
            if (data.success) {
                shippingFee = data.shippingFee;
                document.getElementById('shippingRow').style.display = 'flex';
                document.getElementById('shippingResult').innerHTML = `
                    <div class="alert alert-success">
                        <strong>Địa chỉ:</strong> ${addr}<br>
                        <strong>Khoảng cách:</strong> ${data.distance} km<br>
                        <strong>Phí ship:</strong> ${vi.format(data.shippingFee)} ₫
                    </div>`;
                syncPaymentData();
                saveShippingData(lat, lng, addr, shippingFee);
            } else {
                document.getElementById('shippingResult').innerHTML = `<div class="alert alert-danger">Lỗi: ${data.message}</div>`;
            }
        }

        // CHUYỂN ĐỔI PHƯƠNG THỨC
        document.getElementById('onlinePaymentMethod').addEventListener('change', function () {
            const isVnpay = this.value === 'vnpay';
            document.getElementById('vnpayFormContainer').style.display = isVnpay ? 'block' : 'none';
            document.getElementById('momoFormContainer').style.display = isVnpay ? 'none' : 'block';
            syncPaymentData();
        });

        // KHỞI ĐỘNG
        document.addEventListener('DOMContentLoaded', () => {
            initMap();
            syncPaymentData();
            loadSavedShipping();
            document.getElementById('usePointsCheckbox').addEventListener('change', syncPaymentData);
        });
    </script>
</body>
</html>