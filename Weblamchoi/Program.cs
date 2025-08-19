using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using weblamchoi.Hubs;
using weblamchoi.Models;
using weblamchoi.Services; // Add namespace for ProductService

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index"; // Trang login
        options.AccessDeniedPath = "/Account/AccessDenied"; // Trang lỗi quyền
    });
builder.Services.AddAuthorization();

// Add DbContext with SQL Server
builder.Services.AddDbContext<DienLanhDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add ProductService
builder.Services.AddScoped<ProductService>(); // Register ProductService with Scoped lifetime

// Add SignalR for real-time notifications
builder.Services.AddSignalR();

// Add MVC controllers and views
builder.Services.AddControllersWithViews();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout
    options.Cookie.HttpOnly = true; // Enhance security
    options.Cookie.IsEssential = true; // Make session cookie essential
});

// Build the application
var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DienLanhDbContext>();
    dbContext.Database.Migrate(); // Apply migrations and create DB if not exists
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage(); // Show detailed errors in development
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // Enable session middleware
app.UseAuthentication();
app.UseAuthorization();

// Map default route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map admin-specific route
app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=AdminDashboard}/{action=Index}/{id?}");

// Map SignalR hub for notifications
app.MapHub<NotificationHub>("/notificationHub"); // Ensure NotificationHub exists

app.Run();