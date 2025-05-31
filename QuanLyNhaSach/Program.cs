// ✅ THÊM USING NÀY NẾU CHƯA CÓ Ở ĐẦU FILE
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// ✅ THÊM DÒNG NÀY ĐỂ ĐĂNG KÝ IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Cấu hình Session chi tiết hơn (khuyến nghị)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Thời gian session hết hạn nếu không hoạt động
    options.Cookie.HttpOnly = true;                 // Cookie không thể truy cập bằng JavaScript phía client
    options.Cookie.IsEssential = true;              // Cookie cần thiết cho ứng dụng hoạt động
});
// Dòng builder.Services.AddSession(); của bạn cũng được, nhưng cấu hình thêm options sẽ tốt hơn.

// Nếu bạn có DbContext cần cấu hình động, đây là nơi bạn sẽ thêm nó
// builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) => { ... });


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
// app.UseStaticFiles(); // Cân nhắc thêm dòng này nếu MapStaticAssets không bao gồm tất cả các file tĩnh bạn cần từ wwwroot

app.UseRouting();

app.UseSession();       // Dòng này của bạn đã có và đúng vị trí
// app.UseAuthentication(); // Nếu bạn dùng
app.UseAuthorization();

app.MapStaticAssets(); // Dòng này của bạn
app.MapRazorPages().WithStaticAssets(); // Dòng này của bạn
// Hoặc nếu dùng cách thông thường hơn:
// app.MapRazorPages();
// app.MapControllers(); // Nếu có dùng

app.Run();