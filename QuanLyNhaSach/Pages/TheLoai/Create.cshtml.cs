using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace QuanLyNhaSach.Pages.TheLoai
{
    public class CreateModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        [BindProperty]
        [Required(ErrorMessage = "Tên thể loại không được để trống.")]
        [StringLength(100, ErrorMessage = "Tên thể loại không được vượt quá 100 ký tự.")]
        public string TenTheLoai { get; set; } = "";

        [BindProperty]
        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string? MoTa { get; set; }

        public string? ThongBao { get; set; }

        public IActionResult OnGet()
        {
            var role = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (role != "Quản lý")
            {
                // Thay vì Redirect, có thể set một thông báo và để view xử lý
                // Hoặc nếu có trang AccessDenied chung thì Redirect là tốt.
                // TempData["ErrorMessage"] = "Bạn không có quyền truy cập chức năng này.";
                // return RedirectToPage("/Index"); 
                return RedirectToPage("/AccessDenied"); // Giữ nguyên redirect của bạn
            }
            return Page();
        }

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // For creating a "TheLoai", typically a Manager's task.
                // ActiveConnectionString in their session should be ManagerConnection.
                // If not, it's an error; no fallback for create operations by an admin.
                // The calling method (OnPostAsync) will handle the null.
            }
            return connStr;
        }

        public async Task<IActionResult> OnPostAsync() // Changed to async
        {
            ThongBao = null;

            var role = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (role != "Quản lý")
            {
                ThongBao = "❌ Bạn không có quyền thực hiện chức năng này.";
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = "❌ Lỗi hệ thống: Không thể kết nối đến cơ sở dữ liệu với quyền hạn phù hợp. Vui lòng đăng nhập lại với tài khoản Quản lý.";
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(); // Use async

                // Assuming MaTheLoai is an identity column or handled by a trigger/default in DB
                // If MaTheLoai needs to be provided, it should be a property and included here.
                var cmd = new SqlCommand("INSERT INTO TheLoai (TenTheLoai, MoTa) VALUES (@TenTheLoai, @MoTa)", conn);
                cmd.Parameters.AddWithValue("@TenTheLoai", TenTheLoai);
                cmd.Parameters.AddWithValue("@MoTa", (object?)MoTa ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(); // Use async
                ThongBao = $"✅ Thêm thể loại '{TenTheLoai}' thành công!";

                ModelState.Clear();
                TenTheLoai = string.Empty;
                MoTa = null;
            }
            catch (SqlException sqlEx)
            {
                // Check for duplicate TenTheLoai if it has a unique constraint (e.g., error 2601 or 2627)
                // This depends on your database schema and how you handle uniqueness for TenTheLoai.
                // Example: if (sqlEx.Number == 2601) { ThongBao = $"❌ Lỗi: Tên thể loại '{TenTheLoai}' đã tồn tại."; }
                ThongBao = $"❌ Lỗi SQL khi thêm thể loại: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
            }
            catch (Exception ex)
            {
                ThongBao = "❌ Lỗi không mong muốn: " + ex.Message;
            }

            return Page();
        }
    }
}