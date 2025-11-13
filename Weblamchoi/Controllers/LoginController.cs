using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Models;
using static Org.BouncyCastle.Math.EC.ECCurve;

public class LoginController : Controller
{
    private readonly DienLanhDbContext _context;
    private readonly IConfiguration _config;

    public LoginController(DienLanhDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;

    }

    // Hiển thị form đăng nhập
    [HttpGet]
    public async Task<IActionResult> IndexAsync()
    {
        ViewBag.Categories = await _context.Categories.ToListAsync();

        return View();
    }

    // Xử lý đăng nhập
    [HttpPost]
    public async Task<IActionResult> Login(string username, string password)
    {

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
            return View("Index");
        }

        string passwordHash = HashPassword(password);

        // Check Admin trước
        var admin = _context.Admins.FirstOrDefault(a => a.Username == username && a.PasswordHash == passwordHash);
        if (admin != null)
        {
            await SignInUser(admin.Username, "Admin", admin.AdminID.ToString());
            return RedirectToAction("Index", "AdminDashboard");
        }

        // Check User
        // Check User
        var user = _context.Users.FirstOrDefault(u => u.Email == username && u.PasswordHash == passwordHash);
        if (user != null)
        {
            if (!user.IsActive)
            {
                ViewBag.Error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.";
                return View("Index");
            }

            await SignInUser(user.FullName, "User", user.UserID.ToString());
            return RedirectToAction("Index", "Home");
        }


        ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác.";
        return View("Index");
    }

    // Đăng xuất
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        // 1. Xóa session
        HttpContext.Session.Clear();

        // 2. Xóa cookie xác thực (chỉ xóa cookie login)
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // 3. Xóa cache trình duyệt (ngăn back lại trang cũ)
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        // 4. Thông báo thành công
        TempData["Success"] = "Bạn đã đăng xuất thành công!";

        // 5. Chuyển hướng về trang đăng nhập
        return RedirectToAction("Index", "Login");
    }
    [HttpGet]
    public IActionResult AccessDenied(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> RegisterAsync()
    {
        ViewBag.Categories = await _context.Categories.ToListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Kiểm tra email đã tồn tại trong DB
        if (_context.Users.Any(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Email", "Email đã được sử dụng.");
            return View(model);
        }

        var user = new User
        {
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = HashPassword(model.Password),
            Phone = model.Phone,
            Address = model.Address,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await SignInUser(user.Email, "User", user.UserID.ToString());

        return RedirectToAction("Index", "Home");
    }


    // ================= HELPER =================
    private string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private async Task SignInUser(string username, string role, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.NameIdentifier, userId)
        };

        var claimsIdentity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            ViewBag.Error = "Email không tồn tại trong hệ thống.";
            return View();
        }

        // Tạo token đặt lại mật khẩu
        user.ResetToken = Guid.NewGuid().ToString();
        user.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
        await _context.SaveChangesAsync();

        // Lấy cấu hình SMTP
        var smtpEmail = _config["Smtp:Email"];
        var smtpPassword = _config["Smtp:Password"];
        var smtpHost = _config["Smtp:Host"];
        var smtpPort = int.Parse(_config["Smtp:Port"]);
        var enableSsl = bool.Parse(_config["Smtp:EnableSsl"]);

        // --- Tạo link khôi phục mật khẩu ---
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        // Nếu đang chạy localhost thì tự động thay bằng IP nội bộ (VD: 192.168.x.x)
        if (baseUrl.Contains("localhost"))
        {
            string localIp = "192.168.1.108"; // ⚠️ Thay bằng IP thật của máy bạn (chạy cmd: ipconfig -> IPv4 Address)
            baseUrl = $"{Request.Scheme}://{localIp}:5287";
        }

        // Tạo link hoàn chỉnh
        var resetLink = $"{baseUrl}/Login/ResetPassword?token={user.ResetToken}";


        var mail = new MailMessage();
        mail.From = new MailAddress(smtpEmail, "Điện Lạnh Shop");
        mail.To.Add(user.Email);
        mail.Subject = "Khôi phục mật khẩu";
        mail.IsBodyHtml = true; // 👈 Gửi email dạng HTML

        // Dùng thẻ <a> để Gmail nhận dạng đúng liên kết
        mail.Body = $@"
        <p>Xin chào <b>{user.FullName}</b>,</p>
        <p>Nhấn vào liên kết dưới đây để đặt lại mật khẩu:</p>
        <p><a href='{resetLink}' target='_blank' style='color:#1a73e8'>{resetLink}</a></p>
        <p>Liên kết có hiệu lực trong 15 phút.</p>
        ";
        mail.IsBodyHtml = false;

        using (var smtp = new System.Net.Mail.SmtpClient(smtpHost, smtpPort))
        {
            smtp.Credentials = new System.Net.NetworkCredential(smtpEmail, smtpPassword);
            smtp.EnableSsl = enableSsl;

            try
            {
                await smtp.SendMailAsync(mail);
                ViewBag.Message = "Email khôi phục mật khẩu đã được gửi.";
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Không thể gửi email: {ex.Message}";
            }
        }

        return View();
    }

    // ========== HIỂN THỊ TRANG NHẬP MẬT KHẨU MỚI ==========
    [HttpGet]
    public IActionResult ResetPassword(string token)
    {
        var user = _context.Users.FirstOrDefault(u => u.ResetToken == token && u.ResetTokenExpiry > DateTime.Now);
        if (user == null)
        {
            ViewBag.Error = "Liên kết không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction("Index");
        }

        ViewBag.Token = token;
        return View();
    }

    // ========== CẬP NHẬT MẬT KHẨU MỚI ==========
    [HttpPost]
    public async Task<IActionResult> ResetPassword(string token, string newPassword)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == token && u.ResetTokenExpiry > DateTime.Now);
        if (user == null)
        {
            ViewBag.Error = "Liên kết không hợp lệ hoặc đã hết hạn.";
            return View();
        }

        user.PasswordHash = HashPassword(newPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Mật khẩu của bạn đã được cập nhật thành công.";
        return RedirectToAction("Index", "Login");
    }
}
