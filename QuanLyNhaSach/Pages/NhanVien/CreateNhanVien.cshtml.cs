using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging; // Khuyến khích cho logging
using System; // Đã có
using System.ComponentModel.DataAnnotations; // Cho DataAnnotations

namespace QuanLyNhaSach.Pages.NhanVien
{
    public class CreateNhanVienModel : PageModel
    {
        // ✅ BỎ: private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration (nếu bạn dùng _config thì giữ nguyên)
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<CreateNhanVienModel> _logger;    // ✅ THÊM

        // ✅ SỬA CONSTRUCTOR ĐỂ INJECT DEPENDENCIES
        public CreateNhanVienModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<CreateNhanVienModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            // ✅ BỎ: connStr = _config.GetConnectionString("DefaultConnection");
        }

        // ✅ THÊM DATA ANNOTATIONS CHO VALIDATION
        [BindProperty]
        [Required(ErrorMessage = "Mã nhân viên không được để trống.")]
        [StringLength(5, ErrorMessage = "Mã nhân viên không được vượt quá 5 ký tự.")]
        public string MaNV { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        public string HoTen { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Chức vụ không được để trống.")]
        [StringLength(50, ErrorMessage = "Chức vụ không được vượt quá 50 ký tự.")]
        public string ChucVu { get; set; } = "";

        [BindProperty]
        [DataType(DataType.Date)]
        public DateTime? NgayVaoLam { get; set; } = DateTime.Today; // ✅ Gán giá trị mặc định

        [BindProperty]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(15)]
        public string? SDT { get; set; }

        [BindProperty]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100)]
        public string? Email { get; set; }

        [BindProperty]
        // ✅ CCCD nên có validation (ví dụ: độ dài, chỉ chứa số) và quan trọng là NÊN ĐƯỢC HASH khi lưu
        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD phải có từ 9 đến 12 ký tự.")]
        public string? CCCD { get; set; }

        public string? ThongBao { get; set; } // Giữ lại để hiển thị thông báo chung

        public void OnGet()
        {
            NgayVaoLam = DateTime.Today; // Đảm bảo ngày mặc định khi GET
        }

        // ✅ PHƯƠNG THỨC HELPER ĐỂ LẤY CONNECTION STRING (Tương tự các file trước)
        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            // Chức năng tạo nhân viên thường dành cho "Quản lý".
            // Kỳ vọng "ActiveConnectionString" trong session của họ là "ManagerConnection".
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (!string.IsNullOrEmpty(connStr))
            {
                _logger.LogInformation("CreateNhanVienModel: Using ActiveConnectionString from session.");
                return connStr;
            }
            else
            {
                // Nếu không có trong session, đây là lỗi vì tạo nhân viên cần quyền cụ thể.
                // Không nên fallback về một connection yếu hơn cho thao tác CREATE.
                _logger.LogError("CreateNhanVienModel: ActiveConnectionString not found in session. This operation requires a specific user context (e.g., Manager).");
                return null; // Trả về null để OnPost xử lý lỗi này
            }
        }


        public IActionResult OnPost()
        {
            ThongBao = null; // Xóa thông báo cũ

            // ✅ KIỂM TRA QUYỀN HẠN TRƯỚC
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý") // Giả sử chỉ "Quản lý" mới được tạo nhân viên
            {
                _logger.LogWarning("User với vai trò '{CurrentRole}' đã cố gắng tạo nhân viên. Từ chối truy cập.", currentRole);
                ThongBao = "❌ Bạn không có quyền thực hiện chức năng này.";
                return Page();
            }

            // ✅ SỬ DỤNG ModelState.IsValid thay vì kiểm tra thủ công
            if (!ModelState.IsValid)
            {
                // ModelState đã chứa thông báo lỗi từ DataAnnotations
                // ThongBao = "❌ Vui lòng kiểm tra lại thông tin đã nhập."; // Có thể thêm lỗi chung nếu muốn
                return Page();
            }

            // ✅ BẮT ĐẦU PHẦN SỬA ĐỔI ĐỂ LẤY connStr ĐỘNG
            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = "❌ Lỗi hệ thống: Không thể kết nối đến cơ sở dữ liệu với quyền hạn phù hợp. Vui lòng đăng nhập lại với tài khoản Quản lý.";
                // _logger đã log lỗi trong GetActiveConnectionString
                return Page();
            }
            // ✅ KẾT THÚC PHẦN SỬA ĐỔI ĐỂ LẤY connStr ĐỘNG

            try
            {
                using var conn = new SqlConnection(connStr); // Sử dụng connStr đã được lấy động
                conn.Open();

                // Nhớ rằng sp_ThemNhanVien cũng cần xử lý việc HASH CCCD trước khi lưu
                // và tạo LOGIN/USER SQL tương ứng nếu logic đó nằm trong SP.
                var cmd = new SqlCommand("sp_ThemNhanVien", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@MaNV", MaNV);
                cmd.Parameters.AddWithValue("@HoTen", HoTen);
                cmd.Parameters.AddWithValue("@ChucVu", ChucVu);
                cmd.Parameters.AddWithValue("@NgayVaoLam", (object?)NgayVaoLam ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SDT", string.IsNullOrWhiteSpace(SDT) ? (object)DBNull.Value : SDT); // Kiểm tra IsNullOrWhiteSpace
                cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(Email) ? (object)DBNull.Value : Email);
                cmd.Parameters.AddWithValue("@CCCD", string.IsNullOrWhiteSpace(CCCD) ? (object)DBNull.Value : CCCD); // CCCD NÊN ĐƯỢC HASH

                cmd.ExecuteNonQuery();
                ThongBao = $"✅ Thêm nhân viên {HoTen} (Mã: {MaNV}) thành công!";
                _logger.LogInformation("Nhân viên {MaNV} - {HoTen} đã được thêm thành công bởi {CurrentUser}.", MaNV, HoTen, _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser"));

                // ✅ XÓA DỮ LIỆU FORM SAU KHI THÊM THÀNH CÔNG (TÙY CHỌN)
                ModelState.Clear(); // Xóa trạng thái model để các giá trị không bị giữ lại nếu dùng asp-for
                MaNV = string.Empty;
                HoTen = string.Empty;
                ChucVu = string.Empty; // Giữ lại vai trò mặc định hoặc để trống tùy ý
                NgayVaoLam = DateTime.Today;
                SDT = null;
                Email = null;
                CCCD = null;
            }
            catch (SqlException sqlEx) // ✅ XỬ LÝ LỖI SQL CHI TIẾT HƠN
            {
                _logger.LogError(sqlEx, "Lỗi SQL khi thêm nhân viên {MaNV}. Số lỗi: {ErrorNumber}", MaNV, sqlEx.Number);
                if (sqlEx.Number == 2627 || sqlEx.Number == 2601) // Lỗi trùng khóa chính (MaNV)
                {
                    ThongBao = $"❌ Lỗi: Mã nhân viên '{MaNV}' đã tồn tại.";
                    ModelState.AddModelError("MaNV", ThongBao); // Gắn lỗi vào trường cụ thể
                }
                else
                {
                    ThongBao = $"❌ Lỗi SQL khi thêm nhân viên: {sqlEx.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi thêm nhân viên {MaNV}", MaNV);
                ThongBao = "❌ Lỗi không mong muốn: " + ex.Message;
            }

            return Page(); // Luôn trả về Page để hiển thị ThongBao (thành công hoặc lỗi)
        }
    }
}