using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations; // Cho DataAnnotations
using System.Threading.Tasks; // Cho Task

namespace QuanLyNhaSach.Pages.KhachHang
{
    public class CreateKhachHangModel : PageModel
    {
        // ✅ BỎ private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<CreateKhachHangModel> _logger; // ✅ THÊM

        // ✅ CẬP NHẬT CONSTRUCTOR
        public CreateKhachHangModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<CreateKhachHangModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            // ✅ BỎ connStr = _config.GetConnectionString("DefaultConnection");
        }

        // ✅ THÊM DATA ANNOTATIONS CHO VALIDATION
        [BindProperty]
        [Required(ErrorMessage = "Mã khách hàng không được để trống.")]
        [StringLength(10, ErrorMessage = "Mã khách hàng không được vượt quá 10 ký tự.")]
        public string MaKH { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        public string HoTen { get; set; } = "";

        [BindProperty]
        [StringLength(200, ErrorMessage = "Địa chỉ không được vượt quá 200 ký tự.")]
        public string? DiaChi { get; set; } = "";

        [BindProperty]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(15, ErrorMessage = "Số điện thoại không được vượt quá 15 ký tự.")]
        public string? SDT { get; set; } = "";

        [BindProperty]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
        public string? Email { get; set; } = "";

        // ✅ BỎ SuccessMessage, DÙNG TempData["SuccessMessage"] NHƯ TRONG .CSHTML

        public void OnGet() { }

        // ✅ PHƯƠNG THỨC HELPER ĐỂ LẤY CONNECTION STRING ĐANG HOẠT ĐỘNG
        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            string? connectionStringSource = "Session"; // Để logging

            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (!string.IsNullOrEmpty(connStr))
            {
                _logger.LogInformation("CreateKhachHangModel: Using ActiveConnectionString from session.");
            }
            else
            {
                // Fallback nếu không có trong session.
                // Đối với trang Tạo Khách Hàng, người dùng (ví dụ: Nhân viên) nên đã đăng nhập.
                // ActiveConnectionString NÊN có trong session.
                // Nếu không, có thể dùng connection của Employee hoặc Manager làm mặc định.
                string fallbackKey = "ReadOnlyConnection"; // Hoặc "ManagerConnection", hoặc "DefaultConnection"
                                                           // tùy thuộc vào quyền tối thiểu cần để thêm khách hàng.
                connStr = _configuration.GetConnectionString(fallbackKey);
                connectionStringSource = $"Fallback ({fallbackKey})";
                _logger.LogWarning("CreateKhachHangModel: ActiveConnectionString not found in session. Falling back to '{FallbackKey}'.", fallbackKey);
            }

            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogError("CreateKhachHangModel: Connection string could not be determined (tried session and fallback key used for logging: {ConnectionSource}).", connectionStringSource);
            }
            return connStr;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // ✅ KIỂM TRA MODELSTATE TRƯỚC
            if (!ModelState.IsValid)
            {
                return Page(); // Giữ lại lỗi validation để hiển thị trên form
            }

            // ✅ LẤY CONNECTION STRING ĐỘNG
            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể kết nối đến cơ sở dữ liệu. Vui lòng thử lại sau.");
                _logger.LogError("CreateKhachHang OnPostAsync: Không thể lấy được chuỗi kết nối.");
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_ThemKhachHang", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@MaKH", MaKH);
                cmd.Parameters.AddWithValue("@HoTen", HoTen);
                cmd.Parameters.AddWithValue("@DiaChi", (object?)DiaChi ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SDT", (object?)SDT ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Email", (object?)Email ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Khách hàng {MaKH} - {HoTen} đã được thêm thành công.", MaKH, HoTen);
                TempData["SuccessMessage"] = $"✅ Đã thêm khách hàng {HoTen} (Mã: {MaKH}) thành công.";

                // ✅ XÓA DỮ LIỆU FORM SAU KHI THÊM THÀNH CÔNG
                ModelState.Clear();
                MaKH = string.Empty;
                HoTen = string.Empty;
                DiaChi = string.Empty;
                SDT = string.Empty;
                Email = string.Empty;

                return Page(); // Ở lại trang để có thể thêm tiếp, thông báo thành công sẽ hiển thị
                // Hoặc return RedirectToPage("./Index"); // Nếu bạn muốn chuyển đến danh sách khách hàng
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Lỗi SQL khi thêm khách hàng {MaKH}.", MaKH);
                // Kiểm tra mã lỗi trùng khóa chính (ví dụ 2627 hoặc 2601)
                if (sqlEx.Number == 2627 || sqlEx.Number == 2601)
                {
                    ModelState.AddModelError("MaKH", $"Mã khách hàng '{MaKH}' đã tồn tại.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, $"Lỗi SQL: {sqlEx.Message} (Số lỗi: {sqlEx.Number})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi thêm khách hàng {MaKH}.", MaKH);
                ModelState.AddModelError(string.Empty, $"Lỗi hệ thống: {ex.Message}");
            }

            return Page(); // Trả về trang với lỗi ModelState
        }
    }
}