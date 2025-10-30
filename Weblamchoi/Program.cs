using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using weblamchoi.Models;
using weblamchoi.Services;
using Weblamchoi.Hubs;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1); // Tùy chỉnh thời gian hết hạn cookie
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
// DbContext
builder.Services.AddDbContext<DienLanhDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Scoped services
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<EmailService>();

// HttpClient with Polly retry policy for Grok API
builder.Services.AddHttpClient("xAIClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["xAI:BaseUrl"] ?? "https://api.x.ai/v1");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["xAI:ApiKey"]);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

builder.Services.AddHttpClient(); // Giữ lại để sử dụng cho các dịch vụ khác

// SignalR
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 102400; // Tăng giới hạn kích thước tin nhắn nếu cần
});

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DienLanhDbContext>();
    dbContext.Database.Migrate();
}

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers(); // Hỗ trợ endpoint /AI/GetResponse

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=AdminDashboard}/{action=Index}/{id?}");


// SignalR hubs
app.MapHub<ChatHub>("/chathub");
app.MapHub<weblamchoi.Hubs.NotificationHub>("/notificationHub");

app.Run();