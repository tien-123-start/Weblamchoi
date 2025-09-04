using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Polly;
using System.Security.Cryptography;
using System.Text;
using weblamchoi.Models;
using X.PagedList;

public class AdminController : Controller
{
    private readonly DienLanhDbContext _context;

    public AdminController(DienLanhDbContext context)
    {
        _context = context;
    }


    public async Task<IActionResult> Index(int? page)
    {
        int pageSize = 10; // số admin mỗi trang
        int pageNumber = page ?? 1;

        var query = _context.Admins.OrderBy(a => a.AdminID);

        var total = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();

        var paged = new StaticPagedList<Admin>(items, pageNumber, pageSize, total);

        return View(paged);
    }

public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Admin admin)
    {
        if (ModelState.IsValid)
        {
            admin.PasswordHash = HashPassword(admin.PasswordHash);
            _context.Admins.Add(admin);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        return View(admin);
    }

    public IActionResult Edit(int id)
    {
        var admin = _context.Admins.Find(id);
        if (admin == null) return NotFound();
        return View(admin);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Admin admin)
    {
        if (id != admin.AdminID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existing = _context.Admins.AsNoTracking().FirstOrDefault(a => a.AdminID == id);
                if (existing == null) return NotFound();

                if (existing.PasswordHash != admin.PasswordHash)
                    admin.PasswordHash = HashPassword(admin.PasswordHash);

                _context.Update(admin);
                _context.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Admins.Any(a => a.AdminID == id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(admin);
    }

    public IActionResult Delete(int id)
    {
        var admin = _context.Admins.Find(id);
        if (admin == null) return NotFound();

        _context.Admins.Remove(admin);
        _context.SaveChanges();
        return RedirectToAction(nameof(Index));
    }

    private string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

 
}