using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace QuanLyNhaSach.Pages.KhuyenMai
{
    public class DeleteKhuyenMaiModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DeleteKhuyenMaiModel> _logger;

        public DeleteKhuyenMaiModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<DeleteKhuyenMaiModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string MaKhuyenMai { get; set; } = "";
        public string MoTa { get; set; } = "";

        private string? GetActiveConnectionString(bool isDeleteOperation = false)
        {
            string? connStr = null;
            string? connectionStringSourceInfo = "";

            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
                if (!string.IsNullOrEmpty(connStr))
                {
                    connectionStringSourceInfo = "từ Session (ActiveConnectionString)";
                    _logger.LogInformation("DeleteKhuyenMai: Using {ConnectionStringSource} for (isDelete: {IsDelete})", connectionStringSourceInfo, isDeleteOperation);
                    return connStr;
                }
            }

            // Nếu là thao tác xóa, và không có connection string từ session, đây là vấn đề.
            // User (Quản lý hoặc Nhân viên được phép) cần có connection string phù hợp trong session.
            if (isDeleteOperation)
            {
                _logger.LogError("DeleteKhuyenMai (OnPost): ActiveConnectionString không có trong Session cho thao tác xóa. User cần đăng nhập với vai trò phù hợp (Quản lý hoặc Nhân viên đã được phân quyền).");
                // Fallback về connection string của ManagerConnection nếu bắt buộc phải có cho thao tác xóa,
                // nhưng điều này chỉ nên xảy ra nếu logic đăng nhập đảm bảo Manager luôn dùng ManagerConnection.
                // Tốt nhất là lỗi nếu session không có connection string cho thao tác quan trọng.
                // Tuy nhiên, để code chạy, tạm thời có thể fallback về một connection có quyền xóa (ví dụ Manager)
                // nhưng đây là dấu hiệu của việc session chưa được thiết lập đúng cho vai trò.
                string requiredKeyForDelete = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole") == "Quản lý" ? "ManagerConnection" : "EmployeeConnection";
                connStr = _configuration.GetConnectionString(requiredKeyForDelete);
                if (string.IsNullOrEmpty(connStr))
                {
                    _logger.LogError("DeleteKhuyenMai (OnPost): Không tìm thấy connection string fallback '{RequiredKey}' cho thao tác xóa.", requiredKeyForDelete);
                    return null;
                }
                _logger.LogWarning("DeleteKhuyenMai (OnPost): ActiveConnectionString không có trong Session. Đang sử dụng fallback: {RequiredKey}", requiredKeyForDelete);

            }
            else // Cho OnGet
            {
                string fallbackKey = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole") == "Quản lý" ? "ManagerConnection" : "EmployeeConnection";
                // Nếu cả Quản lý và Nhân viên đều có thể xem trang xóa, thì connection string của họ nên dùng.
                // Nếu không, một ReadOnlyConnection có thể phù hợp hơn cho OnGet.
                // Giả định rằng nếu vào được OnGet thì họ có quyền xem.
                if (string.IsNullOrEmpty(_httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole")))
                { // Chưa đăng nhập
                    fallbackKey = "ReadOnlyConnection"; // Hoặc lỗi nếu trang này yêu cầu đăng nhập
                }

                connStr = _configuration.GetConnectionString(fallbackKey);
                connectionStringSourceInfo = $"từ appsettings.json (Fallback Key: {fallbackKey}) cho OnGet";
                if (string.IsNullOrEmpty(connStr))
                {
                    _logger.LogError("DeleteKhuyenMai (OnGet): Chuỗi kết nối fallback '{FallbackKey}' cũng không tìm thấy.", fallbackKey);
                }
                _logger.LogInformation("DeleteKhuyenMai (OnGet): Sử dụng fallback: {ConnectionStringSource}", connectionStringSourceInfo);
            }
            return connStr;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên")
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập chức năng này.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Mã khuyến mãi không hợp lệ.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }
            MaKhuyenMai = id;

            string? connStr = GetActiveConnectionString(isDeleteOperation: false);

            if (string.IsNullOrEmpty(connStr))
            {
                TempData["ErrorMessage"] = "Lỗi cấu hình kết nối. Không thể tải thông tin khuyến mãi.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("SELECT MoTa FROM KhuyenMai WHERE MaKhuyenMai = @MaKhuyenMai AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                cmd.Parameters.AddWithValue("@MaKhuyenMai", id);
                var moTaResult = await cmd.ExecuteScalarAsync();

                if (moTaResult == null || moTaResult == DBNull.Value)
                {
                    _logger.LogWarning("Không tìm thấy khuyến mãi với Mã: {MaKhuyenMai} để xóa hoặc đã bị ẩn (OnGet).", id);
                    TempData["ErrorMessage"] = $"Không tìm thấy khuyến mãi với Mã: {id} hoặc khuyến mãi này đã được ẩn.";
                    return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
                }
                MoTa = moTaResult.ToString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin khuyến mãi {MaKhuyenMai} để xóa (OnGet).", id);
                TempData["ErrorMessage"] = "Lỗi khi tải thông tin khuyến mãi: " + ex.Message;
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            // SỬA Ở ĐÂY: Cho phép cả "Quản lý" và "Nhân viên" thực hiện xóa
            if (currentRole != "Quản lý" && currentRole != "Nhân viên")
            {
                _logger.LogWarning("User với vai trò '{CurrentRole}' đã cố gắng xóa khuyến mãi. Từ chối truy cập (OnPost).", currentRole);
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            if (string.IsNullOrEmpty(MaKhuyenMai))
            {
                TempData["ErrorMessage"] = "Mã khuyến mãi không được để trống khi xóa.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            string? connStr = GetActiveConnectionString(isDeleteOperation: true);

            if (string.IsNullOrEmpty(connStr))
            {
                TempData["ErrorMessage"] = "Lỗi nghiêm trọng: Không thể xác thực kết nối phù hợp để xóa. Vui lòng đăng nhập lại với vai trò được phép.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_XoaKhuyenMai", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaKhuyenMai", MaKhuyenMai);

                await cmd.ExecuteNonQueryAsync();

                _logger.LogInformation("Khuyến mãi {MaKhuyenMai} đã được ẩn (xóa mềm) thành công bởi user {CurrentUser} (Role: {CurrentRole}).", MaKhuyenMai, _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser"), currentRole);
                TempData["SuccessMessage"] = $"✅ Đã ẩn khuyến mãi '{MaKhuyenMai}' thành công!";
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Lỗi SQL khi xóa khuyến mãi {MaKhuyenMai}.", MaKhuyenMai);
                TempData["ErrorMessage"] = $"Lỗi SQL khi xóa khuyến mãi: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi xóa khuyến mãi {MaKhuyenMai}.", MaKhuyenMai);
                TempData["ErrorMessage"] = "Lỗi hệ thống khi xóa khuyến mãi: " + ex.Message;
            }

            return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
        }
    }
}