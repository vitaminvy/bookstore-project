using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
// using Microsoft.Extensions.Logging; 

namespace QuanLyNhaSach.Pages.KhuyenMai
{
    public class EditKhuyenMaiModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        // private readonly ILogger<EditKhuyenMaiModel> _logger;

        public EditKhuyenMaiModel(IConfiguration configuration,
                                    IHttpContextAccessor httpContextAccessor
                                    /*, ILogger<EditKhuyenMaiModel> logger */)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            // _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        [Required(ErrorMessage = "Mã khuyến mãi là bắt buộc.")]
        public string MaKhuyenMai { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Mô tả không được để trống.")]
        [StringLength(255)]
        public string MoTa { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Ngày bắt đầu không được để trống.")]
        [DataType(DataType.Date)]
        public DateTime NgayBatDau { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Ngày kết thúc không được để trống.")]
        [DataType(DataType.Date)]
        public DateTime NgayKetThuc { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Phần trăm giảm giá không được để trống.")]
        [Range(1, 100, ErrorMessage = "Phần trăm giảm giá phải từ 1 đến 100.")]
        public int PhanTramGiamGia { get; set; }

        private string? GetActiveConnectionString(bool isModificationOperation = false)
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
                string fallbackKey = "ReadOnlyConnection"; // Mặc định cho các thao tác đọc nếu không có session

                if (currentRole == "Quản lý")
                {
                    fallbackKey = "ManagerConnection";
                }
                else if (currentRole == "Nhân viên")
                {
                    fallbackKey = "EmployeeConnection";
                }

                if (isModificationOperation && (currentRole != "Quản lý" && currentRole != "Nhân viên"))
                {
                    // Nếu là thao tác sửa/xóa mà không phải Manager/Employee và session rỗng -> lỗi
                    TempData["ErrorMessage"] = "Lỗi cấu hình kết nối cho vai trò của bạn.";
                    return null;
                }
                else if (isModificationOperation && currentRole == "Nhân viên")
                {
                    // Nhân viên đang thực hiện sửa/xóa, dùng EmployeeConnection
                    fallbackKey = "EmployeeConnection";
                }
                else if (isModificationOperation && currentRole == "Quản lý")
                {
                    fallbackKey = "ManagerConnection";
                }


                connStr = _configuration.GetConnectionString(fallbackKey);
                if (string.IsNullOrEmpty(connStr))
                {
                    TempData["ErrorMessage"] = $"Lỗi cấu hình: Không tìm thấy chuỗi kết nối cho key '{fallbackKey}'.";
                    return null;
                }
            }
            return connStr;
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên") // SỬA Ở ĐÂY
            {
                TempData["ErrorMessage"] = "Bạn không có quyền truy cập chức năng này.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            if (string.IsNullOrEmpty(id))
            {
                TempData["ErrorMessage"] = "Mã khuyến mãi không hợp lệ để sửa.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }
            MaKhuyenMai = id;

            string? connStr = GetActiveConnectionString(isModificationOperation: false);

            if (string.IsNullOrEmpty(connStr))
            {
                if (TempData["ErrorMessage"] == null) TempData["ErrorMessage"] = "Lỗi cấu hình kết nối. Không thể tải thông tin.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("SELECT MaKhuyenMai, MoTa, NgayBatDau, NgayKetThuc, PhanTramGiamGia FROM KhuyenMai WHERE MaKhuyenMai = @MaKhuyenMai AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                cmd.Parameters.AddWithValue("@MaKhuyenMai", id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    MoTa = reader["MoTa"].ToString() ?? "";
                    NgayBatDau = reader["NgayBatDau"] != DBNull.Value ? Convert.ToDateTime(reader["NgayBatDau"]) : DateTime.MinValue;
                    NgayKetThuc = reader["NgayKetThuc"] != DBNull.Value ? Convert.ToDateTime(reader["NgayKetThuc"]) : DateTime.MinValue;
                    PhanTramGiamGia = reader["PhanTramGiamGia"] != DBNull.Value ? Convert.ToInt32(reader["PhanTramGiamGia"]) : 0;
                }
                else
                {
                    TempData["ErrorMessage"] = $"Không tìm thấy khuyến mãi với Mã: {id} hoặc khuyến mãi này đã được ẩn.";
                    return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tải thông tin khuyến mãi: " + ex.Message;
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên") // SỬA Ở ĐÂY
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai"); // Hoặc return Page(); nếu muốn giữ lại dữ liệu form
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (NgayBatDau.Date >= NgayKetThuc.Date)
            {
                ModelState.AddModelError("NgayKetThuc", "Ngày kết thúc phải sau ngày bắt đầu.");
                return Page();
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: true);
            if (string.IsNullOrEmpty(connStr))
            {
                // TempData["ErrorMessage"] đã được set bởi GetActiveConnectionString nếu có lỗi nghiêm trọng
                if (TempData["ErrorMessage"] == null) TempData["ErrorMessage"] = "Lỗi nghiêm trọng: Không thể xác thực kết nối phù hợp để cập nhật.";
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_CapNhatKhuyenMai", conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@MaKhuyenMai", MaKhuyenMai);
                cmd.Parameters.AddWithValue("@MoTa", MoTa);
                cmd.Parameters.AddWithValue("@NgayBatDau", NgayBatDau);
                cmd.Parameters.AddWithValue("@NgayKetThuc", NgayKetThuc);
                cmd.Parameters.AddWithValue("@PhanTramGiamGia", PhanTramGiamGia);

                await cmd.ExecuteNonQueryAsync();
                TempData["SuccessMessage"] = $"✅ Đã cập nhật khuyến mãi '{MoTa}' (Mã: {MaKhuyenMai}) thành công!";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }
            catch (SqlException sqlEx)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi SQL khi cập nhật: {sqlEx.Message} (Số lỗi: {sqlEx.Number})");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Lỗi hệ thống: {ex.Message}");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên") // SỬA Ở ĐÂY (NẾU NHÂN VIÊN ĐƯỢC XÓA)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            if (string.IsNullOrEmpty(MaKhuyenMai))
            {
                TempData["ErrorMessage"] = "Không có mã khuyến mãi nào được chọn để xóa.";
                return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: true);
            if (string.IsNullOrEmpty(connStr))
            {
                if (TempData["ErrorMessage"] == null) TempData["ErrorMessage"] = "Lỗi nghiêm trọng: Không thể xác thực kết nối phù hợp để xóa.";
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
                TempData["SuccessMessage"] = $"✅ Đã ẩn khuyến mãi '{MaKhuyenMai}' thành công!";
            }
            catch (SqlException sqlEx)
            {
                TempData["ErrorMessage"] = $"Lỗi SQL khi xóa khuyến mãi: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi hệ thống khi xóa khuyến mãi: " + ex.Message;
            }
            return RedirectToPage("/KhuyenMai/IndexKhuyenMai");
        }
    }
}