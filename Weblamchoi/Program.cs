using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Headers;
using weblamchoi.Hubs;
using weblamchoi.Models;
using weblamchoi.Services;
using Weblamchoi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ==================== 1. SERVICES ====================
builder.Services.AddControllersWithViews();
// Trong phần services
builder.Services.AddSignalR();
// ==================== 2. SESSION ====================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// ==================== 3. AUTHENTICATION ====================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

// ==================== 4. DATABASE ====================
builder.Services.AddDbContext<DienLanhDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ==================== 5. SCOPED SERVICES ====================
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ==================== 6. HTTP CLIENTS ====================
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
    .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));

builder.Services.AddHttpClient("DefaultClient");

// ==================== 7. CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNgrok", policy =>
    {
        policy.WithOrigins("https://toshia-compressed-nondexterously.ngrok-free.dev")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ==================== 8. SIGNALR ====================
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 102400;
});

// ==================== 9. AUTHORIZATION ====================
builder.Services.AddAuthorization();

// ==================== 10. CONFIG ====================
builder.Services.Configure<MomoSettings>(builder.Configuration.GetSection("Momo"));

// ==================== BUILD ====================
var app = builder.Build();

// ==================== MIGRATIONS ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DienLanhDbContext>();
    db.Database.Migrate();
}

// ==================== MIDDLEWARE ====================
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
// 1. UseRouting() PHẢI ĐỨNG ĐẦU TIÊN TRONG NHÓM NÀY
app.UseRouting();

// ĐÚNG THỨ TỰ
app.UseCors("AllowNgrok");
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// SignalR trước Controller
app.MapHub<ChatHub>("/chathub");
app.MapHub<NotificationHub>("/notificationHub");
app.MapHub<PaymentHub>("/paymentHub");

// Controller + Routes
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "admin",
    pattern: "Admin/{controller=AdminDashboard}/{action=Index}/{id?}");

app.Run();