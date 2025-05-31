using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;
// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging; // Khuyến khích cho logging
using System.Threading.Tasks;     // Đảm bảo có using này cho async/await

namespace QuanLyNhaSach.Pages.NhanVien
{ // ✅ Đảm bảo namespace của bạn đúng
    public class DeleteNhanVienModel : PageModel
    {
        // ✅ BỎ: private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration (nếu bạn dùng _config thì giữ nguyên)
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<DeleteNhanVienModel> _logger;    // ✅ THÊM

        // ✅ SỬA CONSTRUCTOR ĐỂ INJECT DEPENDENCIES
        public DeleteNhanVienModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<DeleteNhanVienModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            // ✅ BỎ: connStr = _config.GetConnectionString("DefaultConnection");
        }

        [BindProperty(SupportsGet = true)] // ✅ Thêm SupportsGet=true để id từ route có thể bind vào MaNV khi OnGet
        public string MaNV { get; set; } = "";
        public string? HoTen { get; set; }
        // public string? ErrorMessage { get; set; } // Có thể dùng TempData hoặc ModelState để báo lỗi

        // ✅ PHƯƠNG THỨC HELPER ĐỂ LẤY CONNECTION STRING
        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            // Chức năng xóa nhân viên thường dành cho "Quản lý".
            // Kỳ vọng "ActiveConnectionString" trong session của họ là "ManagerConnection".
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (!string.IsNullOrEmpty(connStr))
            {
                _logger.LogInformation("DeleteNhanVienModel: Using ActiveConnectionString from session.");
                return connStr;
            }
            else
            {
                // Nếu không có trong session, đây là lỗi vì xóa nhân viên cần quyền cụ thể.
                // Không nên fallback về một connection yếu hơn cho thao tác DELETE.
                _logger.LogError("DeleteNhanVienModel: ActiveConnectionString not found in session. This operation requires a specific user context (e.g., Manager).");
                return null; // Trả về null để các handler xử lý lỗi này
            }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Mã nhân viên không hợp lệ để xóa.";
                // ✅ Cân nhắc redirect về trang danh sách nhân viên thay vì EditNhanVien
                return RedirectToPage("/NhanVien/EditNhanVien"); // Hoặc trang danh sách nhân viên của bạn
            }
            MaNV = id; // Gán id từ route vào MaNV

            // ✅ LẤY connStr ĐỘNG CHO OnGet
            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                TempData["ErrorMessage"] = "Lỗi cấu hình kết nối. Không thể tải thông tin nhân viên để xóa.";
                _logger.LogError("DeleteNhanVien OnGetAsync: Không thể lấy được chuỗi kết nối phù hợp cho Quản lý.");
                return RedirectToPage("/Login"); // Hoặc trang danh sách
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // Lấy thông tin cơ bản để hiển thị xác nhận, đảm bảo nhân viên chưa bị xóa (nếu có cột IsDeleted)
                var cmd = new SqlCommand("SELECT HoTen FROM NhanVien WHERE MaNV = @MaNV", conn);
                // AND (IsDeleted = 0 OR IsDeleted IS NULL) // Nếu có cột IsDeleted
                cmd.Parameters.AddWithValue("@MaNV", id);
                var hoTenResult = await cmd.ExecuteScalarAsync();
                HoTen = hoTenResult?.ToString();

                if (HoTen == null)
                {
                    _logger.LogWarning("Không tìm thấy nhân viên với Mã: {MaNV} để xóa.", id);
                    TempData["ErrorMessage"] = $"Không tìm thấy nhân viên với Mã: {id}.";
                    return RedirectToPage("/NhanVien/EditNhanVien"); // Redirect về trang danh sách nhân viên
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin nhân viên {MaNV} để xóa.", id);
                TempData["ErrorMessage"] = "Lỗi khi tải thông tin nhân viên: " + ex.Message;
                return RedirectToPage("/NhanVien/EditNhanVien"); // Redirect về trang danh sách
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync() // Handler mặc định cho POST, sẽ xóa nhân viên có MaNV từ BindProperty
        {
            // ✅ KIỂM TRA QUYỀN HẠN TRƯỚC KHI THỰC HIỆN THAO TÁC XÓA
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý") // Chỉ "Quản lý" mới được xóa
            {
                _logger.LogWarning("User với vai trò '{CurrentRole}' đã cố gắng xóa nhân viên. Từ chối truy cập.", currentRole);
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                return RedirectToPage("/Login"); // Hoặc trang AccessDenied, hoặc trang danh sách Nhân Viên
            }

            if (string.IsNullOrEmpty(MaNV))
            {
                TempData["ErrorMessage"] = "Mã nhân viên không hợp lệ để xóa.";
                _logger.LogWarning("OnPostAsync DeleteNhanVien: MaNV là null hoặc rỗng.");
                return RedirectToPage("/NhanVien/EditNhanVien");
            }

            // ✅ LẤY connStr ĐỘNG CHO OnPost (yêu cầu nghiêm ngặt connStr của Quản lý từ session)
            string? connStr = GetActiveConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                TempData["ErrorMessage"] = "Lỗi nghiêm trọng: Không thể xác thực kết nối của Quản lý để xóa. Vui lòng đăng nhập lại.";
                _logger.LogError("DeleteNhanVien OnPostAsync: Không thể lấy được chuỗi kết nối phù hợp cho Quản lý để xóa.");
                return RedirectToPage("/Login"); // Hoặc trang Login
            }

            try
            {
                using var conn = new SqlConnection(connStr); // Sử dụng connStr đã được lấy động
                await conn.OpenAsync();

                // Giả sử sp_XoaNhanVien của bạn thực hiện xóa (có thể là xóa mềm hoặc xóa cứng)
                // và có thể cả việc xóa LOGIN/USER SQL tương ứng nếu SP xử lý việc đó.
                var cmd = new SqlCommand("sp_XoaNhanVien", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaNV", MaNV);
                // ✅ Cân nhắc thêm tham số người xóa nếu Stored Procedure của bạn cần
                // var currentUser = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser");
                // cmd.Parameters.AddWithValue("@NguoiXoa", currentUser);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Nhân viên {MaNV} đã được xóa thành công bởi user {CurrentUser}.", MaNV, _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser"));
                TempData["SuccessMessage"] = $"🗑️ Đã xoá nhân viên {MaNV} - {HoTen} thành công.";
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Lỗi SQL khi xóa nhân viên {MaNV}.", MaNV);
                TempData["ErrorMessage"] = $"Lỗi SQL khi xóa nhân viên: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi xóa nhân viên {MaNV}.", MaNV);
                TempData["ErrorMessage"] = "Lỗi hệ thống khi xóa nhân viên: " + ex.Message;
            }

            // ✅ Chuyển hướng về trang danh sách nhân viên sau khi xóa
            return RedirectToPage("/NhanVien/EditNhanVien"); // Hoặc tên trang danh sách nhân viên của bạn
        }
    }
}