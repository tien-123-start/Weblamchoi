using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;

namespace weblamchoi.Controllers.Admin
{
    public class ManufacturersController : Controller
    {
        private readonly DienLanhDbContext _context;

        public ManufacturersController(DienLanhDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Manufacturers.ToListAsync());
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Manufacturer manufacturer)
        {
            if (ModelState.IsValid)
            {
                _context.Manufacturers.Add(manufacturer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(manufacturer);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var manufacturer = await _context.Manufacturers.FindAsync(id);
            if (manufacturer == null) return NotFound();
            return View(manufacturer);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, Manufacturer updatedManufacturer)
        {
            if (id != updatedManufacturer.ManufacturerID) return NotFound();

            var manufacturer = await _context.Manufacturers.FindAsync(id);
            if (manufacturer == null) return NotFound();

            manufacturer.ManufacturerName = updatedManufacturer.ManufacturerName;
            manufacturer.Country = updatedManufacturer.Country;
            manufacturer.Website = updatedManufacturer.Website;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var manufacturer = await _context.Manufacturers.FindAsync(id);
            if (manufacturer == null) return NotFound();

            _context.Manufacturers.Remove(manufacturer);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
