using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using weblamchoi.Models;
using weblamchoi.Services;

public class LoginMailServices : Controller
{
    private readonly EmailService _emailService;
    private readonly DienLanhDbContext _context;

    public LoginMailServices(EmailService emailService, DienLanhDbContext context)
    {
        _emailService = emailService;
        _context = context;
    }

    [HttpGet]
    public IActionResult Index() => View();

    // Bước 1: Nhập email, gửi OTP
    [HttpPost]
    public async Task<IActionResult> LoginWithEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            ViewBag.Error = "Vui lòng nhập email.";
            return View("Index");
        }

        // Tạo OTP
        var otp = new Random().Next(100000, 999999).ToString();

        // Tạo cookie tạm thời lưu OTP và email
        var tempClaims = new List<Claim>
        {
            new Claim("TempEmail", email),
            new Claim("TempOTP", otp)
        };

        var tempIdentity = new ClaimsIdentity(tempClaims, CookieAuthenticationDefaults.AuthenticationScheme);
        var tempPrincipal = new ClaimsPrincipal(tempIdentity);

        var authProps = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, tempPrincipal, authProps);

        // Gửi OTP qua mail
        await _emailService.SendEmailAsync(email, "Mã OTP đăng nhập", $"Mã OTP của bạn là: <b>{otp}</b>");

        ViewBag.Message = "Mã OTP đã được gửi về email. Vui lòng nhập để xác thực.";
        return View("VerifyOtp");
    }

    // Bước 2: Xác thực OTP
    [HttpPost]
    public async Task<IActionResult> VerifyOtp(string otp)
    {
        var tempEmail = User.FindFirst("TempEmail")?.Value;
        var tempOtp = User.FindFirst("TempOTP")?.Value;

        if (string.IsNullOrEmpty(tempEmail) || string.IsNullOrEmpty(tempOtp) || otp != tempOtp)
        {
            ViewBag.Error = "Mã OTP không đúng hoặc đã hết hạn.";
            return View("VerifyOtp");
        }

        // Xóa cookie OTP tạm thời
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Kiểm tra user trong DB
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == tempEmail);
        if (user == null)
        {
            // Nếu chưa có, tạo mới
            user = new User
            {
                Email = tempEmail,
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // Tạo claim chính thức
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()) // Quan trọng để đồng bộ
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        return RedirectToAction("Index", "Home");
    }

    // Đăng xuất
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
