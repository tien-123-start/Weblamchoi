using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using weblamchoi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace weblamchoi.Controllers
{
    public class ContactController : Controller
    {
        private readonly DienLanhDbContext _context;

        public ContactController(DienLanhDbContext context)
        {
            _context = context;
        }

        // GET: /Contact
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Contact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Contact contact)
        {
            if (ModelState.IsValid)
            {
                contact.SubmittedAt = DateTime.Now;
                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();
                TempData["ContactMessage"] = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi sớm nhất có thể.";
                return RedirectToAction("Index");
            }

            TempData["ContactError"] = "Vui lòng điền đầy đủ thông tin hợp lệ.";
            return View(contact);
        }

        // GET: /Contact/Admin
        public async Task<IActionResult> Admin()
        {
            var contacts = await _context.Contacts
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();
            return View(contacts);
        }

        // GET: /Contact/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // GET: /Contact/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(m => m.Id == id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        // POST: /Contact/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
                TempData["ContactMessage"] = "Đã xóa liên hệ thành công.";
            }
            else
            {
                TempData["ContactError"] = "Không tìm thấy liên hệ để xóa.";
            }
            return RedirectToAction(nameof(Admin));
        }
    }
}