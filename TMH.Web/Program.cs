using TMH.Web.Services;

// ============================================================
// Program.cs — Điểm khởi động của TMH.Web (Web App)
//
// Web App KHÔNG kết nối SQL Server trực tiếp.
// Nó chỉ giao tiếp với TMH.API qua HTTP (HttpClient).
// Điều này đảm bảo tách biệt hoàn toàn:
//   - API lo xử lý dữ liệu, bảo mật, business logic
//   - Web App lo render giao diện, quản lý session
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// --- MVC với Razor Views ---
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.DictionaryKeyPolicy  = System.Text.Json.JsonNamingPolicy.CamelCase;
        // Bắt buộc: cho phép [FromBody] deserialize enum từ string ("DaDen" → AppointmentStatus.DaDen)
        // Không có dòng này, JS gửi "DaDen" lên StaffController/DoctorController sẽ bị bind lỗi
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// --- Session: lưu JWT token và thông tin user sau khi đăng nhập ---
// Session được lưu phía server (Memory). Trong production nên dùng Redis
// để session tồn tại khi restart server hoặc scale nhiều instance.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromHours(8);   // Khớp với ExpireHours của JWT
    options.Cookie.HttpOnly    = true;   // JS không đọc được cookie session — chống XSS
    options.Cookie.IsEssential = true;  // Không cần user consent theo GDPR
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
});

// IHttpContextAccessor cho phép ApiService đọc Session từ bên trong service layer
// (không inject trực tiếp HttpContext vào service vì nó không testable)
builder.Services.AddHttpContextAccessor();

// --- HttpClient cho ApiService ---
// AddHttpClient<T>() tạo một named HttpClient được quản lý vòng đời bởi IHttpClientFactory.
// Tránh dùng "new HttpClient()" trong code vì có thể gây socket exhaustion.
builder.Services.AddHttpClient<ApiService>(client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
                    ?? "https://localhost:7100/";  // URL của TMH.API khi chạy development
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Named HttpClient cho MessageProxyController
builder.Services.AddHttpClient("ApiClient", client =>
{
    var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
                    ?? "https://localhost:7100/";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();    // Phục vụ wwwroot/css, wwwroot/js...
app.UseRouting();

app.UseSession();        // Session phải đứng trước MapControllerRoute để controller đọc được

// Không dùng JWT middleware ở Web App vì xác thực đã được xử lý hoàn toàn phía API.
// Web App chỉ lưu/đọc token từ Session và đính vào header khi gọi API.

app.MapControllerRoute(
    name:    "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
