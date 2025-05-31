using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace QuanLyNhaSach.Pages.Api.Sach
{
    public class GetDonGiaModel : PageModel
    {
        // ✅ BỎ private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration CHO NHẤT QUÁN
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<GetDonGiaModel> _logger; // ✅ THÊM

        // ✅ CẬP NHẬT CONSTRUCTOR
        public GetDonGiaModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<GetDonGiaModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            // ✅ BỎ connStr = _config.GetConnectionString("DefaultConnection");
        }

        public async Task<IActionResult> OnGetAsync(string ma)
        {
            string? connStr = null; // ✅ KHỞI TẠO connStr Ở ĐÂY

            // ✅ BẮT ĐẦU SỬA: LẤY CONNECTION STRING ĐỘNG
            try
            {
                if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
                {
                    connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
                }

                if (!string.IsNullOrEmpty(connStr))
                {
                    _logger.LogInformation("GetDonGia API using connection string from session for MaSach: {MaSach}", ma);
                }
                else
                {
                    // Fallback nếu không có trong session (ví dụ: API được gọi bởi client không có session, hoặc người dùng chưa đăng nhập)
                    // Sử dụng một connection string mặc định, có thể là ReadOnly nếu API này chỉ đọc
                    string fallbackKey = "ReadOnlyConnection"; // HOẶC "DefaultConnection" tùy bạn cấu hình
                    connStr = _configuration.GetConnectionString(fallbackKey);
                    _logger.LogWarning("ActiveConnectionString not found in session for GetDonGia API (MaSach: {MaSach}). Falling back to: '{FallbackKey}'", ma, fallbackKey);
                }

                if (string.IsNullOrEmpty(connStr))
                {
                    _logger.LogError("Connection string could not be determined for GetDonGia API (MaSach: {MaSach}).", ma);
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Lỗi cấu hình máy chủ: Không thể xác định chuỗi kết nối." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving connection string configuration in GetDonGia API for MaSach: {MaSach}", ma);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Lỗi cấu hình máy chủ." });
            }
            // ✅ KẾT THÚC SỬA: LẤY CONNECTION STRING ĐỘNG

            if (string.IsNullOrEmpty(ma))
            {
                return BadRequest(new { error = "Mã sách không được để trống." });
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                var cmd = new SqlCommand("SELECT GiaBia FROM Sach WHERE MaSach = @maParam", conn); // ✅ ĐỔI TÊN THAM SỐ
                cmd.Parameters.AddWithValue("@maParam", ma); // ✅ ĐỔI TÊN THAM SỐ

                await conn.OpenAsync();
                var gia = await cmd.ExecuteScalarAsync();

                if (gia == null || gia == DBNull.Value) // ✅ KIỂM TRA DBNull.Value
                {
                    _logger.LogWarning("MaSach {MaSach} not found or GiaBia is null.", ma);
                    return NotFound(new { error = $"Không tìm thấy sách hoặc giá bìa cho mã: {ma}" });
                }

                return new JsonResult(new { giaBia = Convert.ToDecimal(gia) });
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL error in GetDonGia API for MaSach: {MaSach}", ma);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Lỗi truy vấn cơ sở dữ liệu." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error in GetDonGia API for MaSach: {MaSach}", ma);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Đã có lỗi xảy ra." });
            }
        }
    }
}