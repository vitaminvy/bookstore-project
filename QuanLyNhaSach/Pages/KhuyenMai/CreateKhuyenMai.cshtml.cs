using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System; // Đã có
using System.Threading.Tasks; // ✅ THÊM CHO ASYNC
using System.ComponentModel.DataAnnotations; // ✅ THÊM CHO VALIDATION

namespace QuanLyNhaSach.Pages.KhuyenMai
{ // ✅ Thêm namespace lồng cho nhất quán nếu cần
    public class CreateKhuyenMaiModel : PageModel
    {
        // ✅ BỎ private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<CreateKhuyenMaiModel> _logger; // ✅ THÊM

        // ✅ CẬP NHẬT CONSTRUCTOR
        public CreateKhuyenMaiModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<CreateKhuyenMaiModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            // ✅ BỎ connStr = _config.GetConnectionString("DefaultConnection");
        }

        // ✅ THÊM DATA ANNOTATIONS CHO VALIDATION
        [BindProperty]
        [Required(ErrorMessage = "Mã khuyến mãi không được để trống.")]
        [StringLength(10, ErrorMessage = "Mã khuyến mãi không được vượt quá 10 ký tự.")]
        public string MaKhuyenMai { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Mô tả không được để trống.")]
        [StringLength(255, ErrorMessage = "Mô tả không được vượt quá 255 ký tự.")]
        public string MoTa { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Ngày bắt đầu không được để trống.")]
        [DataType(DataType.Date)]
        public DateTime NgayBatDau { get; set; } = DateTime.Today; // ✅ Gán giá trị mặc định

        [BindProperty]
        [Required(ErrorMessage = "Ngày kết thúc không được để trống.")]
        [DataType(DataType.Date)]
        public DateTime NgayKetThuc { get; set; } = DateTime.Today.AddDays(7); // ✅ Gán giá trị mặc định

        [BindProperty]
        [Required(ErrorMessage = "Phần trăm giảm giá không được để trống.")]
        [Range(1, 100, ErrorMessage = "Phần trăm giảm giá phải từ 1 đến 100.")]
        public int PhanTramGiamGia { get; set; }

        public IActionResult OnGet()
        {
            // Khởi tạo giá trị mặc định cho ngày nếu cần thiết khi GET
            // NgayBatDau = DateTime.Today;
            // NgayKetThuc = DateTime.Today.AddDays(7); // Ví dụ: khuyến mãi 7 ngày
            return Page();
        }

        // ✅ PHƯƠNG THỨC HELPER ĐỂ LẤY CONNECTION STRING ĐANG HOẠT ĐỘNG
        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            // Đối với chức năng tạo khuyến mãi, thường chỉ "Quản lý" mới có quyền.
            // Chúng ta kỳ vọng "ActiveConnectionString" trong session của họ là "ManagerConnection".
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (!string.IsNullOrEmpty(connStr))
            {
                _logger.LogInformation("CreateKhuyenMaiModel: Using ActiveConnectionString from session.");
            }
            else
            {
                // Nếu không có trong session, đây có thể là lỗi nghiêm trọng vì tạo khuyến mãi cần quyền cụ thể.
                // Không nên fallback về một connection yếu hơn cho thao tác CREATE.
                _logger.LogError("CreateKhuyenMaiModel: ActiveConnectionString not found in session. This operation requires a specific user context (e.g., Manager).");
                // connStr sẽ vẫn là null, và sẽ được xử lý ở nơi gọi.
                // Hoặc, bạn có thể ném một exception ở đây nếu muốn.
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

            // Kiểm tra ràng buộc ngày (bạn đã có, rất tốt)
            if (NgayBatDau.Date >= NgayKetThuc.Date) // ✅ So sánh phần Date để bỏ qua Time
            {
                ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc phải sau ngày bắt đầu.");
                //ModelState.AddModelError(string.Empty, "❌ Ngày bắt đầu phải trước ngày kết thúc."); // Hoặc lỗi chung
                return Page();
            }

            // ✅ KIỂM TRA QUYỀN HẠN (NẾU CHỨC NĂNG NÀY CHỈ DÀNH CHO QUẢN LÝ)
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý") // Giả sử chỉ Quản lý mới được tạo khuyến mãi
            {
                _logger.LogWarning("User with role '{UserRole}' attempted to create a promotion. Denied.", currentRole);
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện chức năng này."; // Dùng TempData để hiển thị lỗi sau redirect hoặc trên Page()
                return Page(); // Hoặc RedirectToPage("/AccessDenied");
            }


            // ✅ LẤY CONNECTION STRING ĐỘNG
            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ModelState.AddModelError(string.Empty, "Lỗi hệ thống: Không thể kết nối đến cơ sở dữ liệu với quyền hạn phù hợp. Vui lòng đăng nhập lại với tài khoản Quản lý.");
                _logger.LogError("CreateKhuyenMai OnPostAsync: Không thể lấy được chuỗi kết nối phù hợp cho Quản lý.");
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_ThemKhuyenMai", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@MaKhuyenMai", MaKhuyenMai);
                cmd.Parameters.AddWithValue("@MoTa", MoTa);
                cmd.Parameters.AddWithValue("@NgayBatDau", NgayBatDau);
                cmd.Parameters.AddWithValue("@NgayKetThuc", NgayKetThuc);
                cmd.Parameters.AddWithValue("@PhanTramGiamGia", PhanTramGiamGia);
                // ✅ Thêm tham số @NguoiTao nếu Stored Procedure của bạn cần
                // var currentUser = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser");
                // cmd.Parameters.AddWithValue("@NguoiTao", (object)currentUser ?? DBNull.Value);


                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Khuyến mãi {MaKhuyenMai} - {MoTa} đã được thêm thành công bởi {CurrentUser}.", MaKhuyenMai, MoTa, _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser"));
                TempData["SuccessMessage"] = $"✅ Đã thêm khuyến mãi '{MoTa}' (Mã: {MaKhuyenMai}) thành công!";

                // ✅ Reset form sau khi thành công
                ModelState.Clear();
                MaKhuyenMai = string.Empty;
                MoTa = string.Empty;
                NgayBatDau = DateTime.Today;
                NgayKetThuc = DateTime.Today.AddDays(7);
                PhanTramGiamGia = 0;

                return Page(); // Ở lại trang để có thể thêm tiếp, thông báo thành công sẽ hiển thị
                // Hoặc return RedirectToPage("./Index"); // Nếu bạn muốn chuyển đến danh sách khuyến mãi
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Lỗi SQL khi thêm khuyến mãi {MaKhuyenMai}.", MaKhuyenMai);
                // Kiểm tra mã lỗi trùng khóa chính (ví dụ 2627 hoặc 2601)
                if (sqlEx.Number == 2627 || sqlEx.Number == 2601)
                {
                    ModelState.AddModelError("MaKhuyenMai", $"Mã khuyến mãi '{MaKhuyenMai}' đã tồn tại.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, $"Lỗi SQL: {sqlEx.Message} (Số lỗi: {sqlEx.Number})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi thêm khuyến mãi {MaKhuyenMai}.", MaKhuyenMai);
                ModelState.AddModelError(string.Empty, $"Lỗi hệ thống: {ex.Message}");
            }

            return Page(); // Trả về trang với lỗi ModelState
        }
    }
}