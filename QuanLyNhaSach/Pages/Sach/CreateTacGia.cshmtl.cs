using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations; // Required for DataAnnotations
using System.Threading.Tasks; // Required for async Task

namespace QuanLyNhaSach.Pages.Sach // Assuming this is under Sach or a similar folder
{
    public class CreateTacGiaModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateTacGiaModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        [BindProperty]
        [Required(ErrorMessage = "Mã tác giả không được để trống.")]
        [StringLength(10, ErrorMessage = "Mã tác giả không được vượt quá 10 ký tự.")]
        public string MaTacGia { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Họ tên tác giả không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        public string HoTen { get; set; } = "";

        [BindProperty]
        [StringLength(1000, ErrorMessage = "Tiểu sử không được vượt quá 1000 ký tự.")]
        public string? TieuSu { get; set; }

        [BindProperty]
        [Range(1000, 2100, ErrorMessage = "Năm sinh không hợp lệ.")]
        public int? NamSinh { get; set; }

        [BindProperty]
        [StringLength(100, ErrorMessage = "Quê quán không được vượt quá 100 ký tự.")]
        public string? QueQuan { get; set; }

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
                // For creating an author, typically a Manager's task.
                // If ActiveConnectionString isn't in session, it's an issue.
                // Fallback to ManagerConnection from config if session is not set up as expected.
                string fallbackKey = "ReadOnlyConnection";
                connStr = _configuration.GetConnectionString(fallbackKey);

                if (string.IsNullOrEmpty(connStr))
                {
                    // Log critical error: Connection string for manager/create author is missing
                }
                else
                {
                    // Log warning: Using fallback connection string
                }
            }
            return connStr;
        }

        public async Task<IActionResult> OnPostAsync() // Changed to async Task<IActionResult>
        {
            ThongBao = null;

            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                ThongBao = "❌ Bạn không có quyền thực hiện chức năng này.";
                return Page();
            }

            if (!ModelState.IsValid) // Using DataAnnotations for validation
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
                await conn.OpenAsync(); // Use async open

                var cmd = new SqlCommand("sp_ThemTacGia", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@MaTacGia", MaTacGia);
                cmd.Parameters.AddWithValue("@HoTen", HoTen);
                cmd.Parameters.AddWithValue("@TieuSu", (object?)TieuSu ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NamSinh", (object?)NamSinh ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@QueQuan", (object?)QueQuan ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync(); // Use async execute
                ThongBao = $"✅ Thêm tác giả '{HoTen}' (Mã: {MaTacGia}) thành công!";

                ModelState.Clear();
                MaTacGia = string.Empty;
                HoTen = string.Empty;
                TieuSu = null;
                NamSinh = null;
                QueQuan = null;
            }
            catch (SqlException sqlEx)
            {
                if (sqlEx.Number == 2627 || sqlEx.Number == 2601) // Duplicate key error
                {
                    ThongBao = $"❌ Lỗi: Mã tác giả '{MaTacGia}' đã tồn tại.";
                    ModelState.AddModelError("MaTacGia", ThongBao);
                }
                else
                {
                    ThongBao = $"❌ Lỗi SQL khi thêm tác giả: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
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