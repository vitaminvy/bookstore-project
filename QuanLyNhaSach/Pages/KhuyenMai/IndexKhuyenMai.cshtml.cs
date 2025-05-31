using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging; // Tùy chọn, nhưng khuyến khích cho logging
using System.Collections.Generic;  // Cho List<T>
using System.Threading.Tasks;    // Cho Task

namespace QuanLyNhaSach.Pages.KhuyenMai
{ // ✅ Đảm bảo namespace của bạn đúng
    public class IndexModel : PageModel
    {
        // ✅ BỎ: private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration (nếu bạn dùng _config thì giữ nguyên)
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<IndexModel> _logger;             // ✅ THÊM (Tùy chọn)

        // ✅ SỬA CONSTRUCTOR ĐỂ INJECT DEPENDENCIES
        public IndexModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<IndexModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger; // ✅ THÊM (Tùy chọn)
            // ✅ BỎ: connStr = _config.GetConnectionString("DefaultConnection");
        }

        public List<KhuyenMaiItem> KhuyenMais { get; set; } = new List<KhuyenMaiItem>();
        public string? ErrorMessage { get; set; } // ✅ THÊM: Để hiển thị lỗi (nếu có)

        public class KhuyenMaiItem // Giữ nguyên class này
        {
            public string MaKhuyenMai { get; set; } = "";
            public string MoTa { get; set; } = "";
            public DateTime NgayBatDau { get; set; }
            public DateTime NgayKetThuc { get; set; }
            public int PhanTramGiamGia { get; set; }
            // ✅ THÊM: Có thể bạn muốn thêm cột IsDeleted để hiển thị trạng thái nếu cần
            // public bool IsDeleted { get; set; }
        }

        public async Task OnGetAsync()
        {
            // ✅ BẮT ĐẦU PHẦN SỬA ĐỔI ĐỂ LẤY connStr ĐỘNG
            string? connStr = null;
            string? connectionStringSourceInfo = ""; // Để biết connStr lấy từ đâu

            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
                if (!string.IsNullOrEmpty(connStr))
                {
                    connectionStringSourceInfo = "từ Session (ActiveConnectionString)";
                }
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // Fallback nếu không có trong session (ví dụ: người dùng chưa đăng nhập, hoặc khách truy cập công khai)
                // Sử dụng một connection string có quyền đọc, ví dụ "ReadOnlyConnection" hoặc "DefaultConnection"
                string fallbackKey = "ReadOnlyConnection"; // Hoặc "DefaultConnection" nếu nó có quyền đọc
                connStr = _configuration.GetConnectionString(fallbackKey);
                connectionStringSourceInfo = $"từ appsettings.json (Fallback Key: {fallbackKey})";

                if (string.IsNullOrEmpty(connStr))
                {
                    ErrorMessage = $"Lỗi hệ thống: Không thể xác định chuỗi kết nối (đã thử Session và Fallback Key: '{fallbackKey}').";
                    _logger?.LogError("IndexKhuyenMai: Không thể lấy chuỗi kết nối từ Session hoặc Fallback ('{FallbackKey}').", fallbackKey);
                    return; // Không thể làm gì nếu không có connection string
                }
                _logger?.LogWarning("IndexKhuyenMai: ActiveConnectionString không có trong Session. Đang sử dụng fallback: {FallbackKey}", fallbackKey);
            }
            _logger?.LogInformation("IndexKhuyenMai sử dụng connection string {Source}", connectionStringSourceInfo);
            // ✅ KẾT THÚC PHẦN SỬA ĐỔI ĐỂ LẤY connStr ĐỘNG

            try // ✅ THÊM TRY-CATCH BAO QUANH THAO TÁC CSDL
            {
                using var conn = new SqlConnection(connStr); // Sử dụng connStr đã được lấy động
                await conn.OpenAsync();

                // Lọc khuyến mãi chưa bị ẩn (IsDeleted = 0). Cân nhắc chỉ SELECT các cột cần thiết.
                var cmd = new SqlCommand("SELECT MaKhuyenMai, MoTa, NgayBatDau, NgayKetThuc, PhanTramGiamGia FROM KhuyenMai WHERE IsDeleted = 0 OR IsDeleted IS NULL ORDER BY NgayKetThuc DESC, NgayBatDau DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    KhuyenMais.Add(new KhuyenMaiItem
                    {
                        MaKhuyenMai = reader["MaKhuyenMai"].ToString() ?? "", // ✅ Xử lý null cho ToString()
                        MoTa = reader["MoTa"].ToString() ?? "",
                        NgayBatDau = reader["NgayBatDau"] != DBNull.Value ? (DateTime)reader["NgayBatDau"] : DateTime.MinValue,
                        NgayKetThuc = reader["NgayKetThuc"] != DBNull.Value ? (DateTime)reader["NgayKetThuc"] : DateTime.MinValue,
                        PhanTramGiamGia = reader["PhanTramGiamGia"] != DBNull.Value ? (int)reader["PhanTramGiamGia"] : 0
                    });
                }
            }
            catch (SqlException sqlEx) // ✅ XỬ LÝ LỖI SQL
            {
                ErrorMessage = $"Lỗi truy vấn cơ sở dữ liệu: {sqlEx.Message}";
                _logger?.LogError(sqlEx, "IndexKhuyenMai: Lỗi SQL khi lấy danh sách khuyến mãi.");
            }
            catch (Exception ex) // ✅ XỬ LÝ LỖI CHUNG
            {
                ErrorMessage = $"Đã có lỗi xảy ra: {ex.Message}";
                _logger?.LogError(ex, "IndexKhuyenMai: Lỗi không mong muốn khi lấy danh sách khuyến mãi.");
            }
        }
    }
}