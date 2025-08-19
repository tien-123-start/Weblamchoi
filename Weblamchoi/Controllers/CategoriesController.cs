using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;

namespace weblamchoi.Controllers.Admin
{
    public class CategoriesController : Controller
    {
        private readonly DienLanhDbContext _context;

        public CategoriesController(DienLanhDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Categories.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, Category updatedCategory)
        {
            if (id != updatedCategory.CategoryID) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            category.CategoryName = updatedCategory.CategoryName;
            category.Description = updatedCategory.Description;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
