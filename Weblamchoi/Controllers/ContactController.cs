using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using weblamchoi.Models;
using weblamchoi.Services;
using X.PagedList;
using X.PagedList.Extensions;

namespace weblamchoi.Controllers
{
    public class ContactController : Controller
    {
        private readonly IContactService _contactService;
        private readonly DienLanhDbContext _context;

        public ContactController(IContactService contactService, DienLanhDbContext context)
        {
            _contactService = contactService;
            _context = context;
        }

        // GET: /Contact
        public async Task<IActionResult> Index()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            return View();
        }

        // POST: /Contact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Contact contact)
        {
            if (ModelState.IsValid)
            {
                await _contactService.AddAsync(contact);
                TempData["ContactMessage"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất có thể.";
                return RedirectToAction("Index");
            }
            ViewBag.Categories = await _context.Categories.ToListAsync();

            TempData["ContactError"] = "Vui lòng điền đầy đủ thông tin hợp lệ.";
            return View(contact);
        }

        // GET: /Contact/Admin
        public async Task<IActionResult> Admin(int? page)
        {
            int pageSize = 10; // số liên hệ mỗi trang
            int pageNumber = page ?? 1;

            var contacts = await _contactService.GetAllAsync();

            // Nếu không có CreatedAt, ta sắp xếp theo ContactID giảm dần
            var pagedContacts = contacts
                .OrderByDescending(c => c.Id)
                .ToPagedList(pageNumber, pageSize);

            return View(pagedContacts);
        }

        // GET: /Contact/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var contact = await _contactService.GetByIdAsync(id.Value);
            if (contact == null) return NotFound();

            return View(contact);
        }

        // GET: /Contact/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var contact = await _contactService.GetByIdAsync(id.Value);
            if (contact == null) return NotFound();

            return View(contact);
        }

        // POST: /Contact/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _contactService.DeleteAsync(id);
            TempData["ContactMessage"] = "Đã xóa liên hệ thành công.";
            return RedirectToAction(nameof(Admin));
        }
    }

}