using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Models;

public class LoginController : Controller
{
    private readonly DienLanhDbContext _context;

    public LoginController(DienLanhDbContext context)
    {
        _context = context;
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
        var user = _context.Users.FirstOrDefault(u => u.Email == username && u.PasswordHash == passwordHash);
        if (user != null)
        {
            await SignInUser(user.Email, "User", user.UserID.ToString());
            return RedirectToAction("Index", "Home");
        }

        ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không chính xác.";
        return View("Index");
    }

    // Đăng xuất
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
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
            return View(model);

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
        _context.SaveChanges();

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
}
