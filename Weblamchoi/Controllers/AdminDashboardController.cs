using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using weblamchoi.Models;

namespace weblamchoi.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly DienLanhDbContext _context;

        public AdminDashboardController(DienLanhDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
