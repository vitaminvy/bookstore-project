using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace QuanLyNhaSach.Pages.Sach // Hoặc namespace QuanLyNhaSach.Pages.TheLoai tùy bạn tổ chức
{
    public class CreateTheLoaiModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateTheLoaiModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        [BindProperty]
        [Required(ErrorMessage = "Mã thể loại không được để trống.")]
        [StringLength(10, ErrorMessage = "Mã thể loại không được vượt quá 10 ký tự.")]
        public string MaTheLoai { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Tên thể loại không được để trống.")]
        [StringLength(100, ErrorMessage = "Tên thể loại không được vượt quá 100 ký tự.")]
        public string TenTheLoai { get; set; } = "";

        [BindProperty]
        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string? MoTa { get; set; }

        public string? ThongBao { get; set; }

        public void OnGet()
        {
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
                string fallbackKey = "ReadOnlyConnection"; // Tạo thể loại thường là quyền của Manager
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        public async Task<IActionResult> OnPostAsync() // Chuyển sang async Task
        {
            ThongBao = null;

            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
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
                ThongBao = "❌ Lỗi hệ thống: Không thể kết nối đến cơ sở dữ liệu với quyền hạn phù hợp.";
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(); // Sử dụng async open

                var cmd = new SqlCommand("sp_ThemTheLoai", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@MaTheLoai", MaTheLoai);
                cmd.Parameters.AddWithValue("@TenTheLoai", TenTheLoai);
                cmd.Parameters.AddWithValue("@MoTa", (object?)MoTa ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(); // Sử dụng async execute
                ThongBao = $"✅ Thêm thể loại '{TenTheLoai}' (Mã: {MaTheLoai}) thành công!";

                ModelState.Clear();
                MaTheLoai = string.Empty;
                TenTheLoai = string.Empty;
                MoTa = null;
            }
            catch (SqlException sqlEx)
            {
                if (sqlEx.Number == 2627 || sqlEx.Number == 2601) // Lỗi trùng khóa
                {
                    ThongBao = $"❌ Lỗi: Mã thể loại '{MaTheLoai}' đã tồn tại.";
                    ModelState.AddModelError("MaTheLoai", ThongBao);
                }
                else
                {
                    ThongBao = $"❌ Lỗi SQL khi thêm thể loại: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
                }
            }
            catch (Exception ex)
            {
                ThongBao = "❌ Lỗi không mong muốn: " + ex.Message;
            }

            return Page();
        }
    }
}