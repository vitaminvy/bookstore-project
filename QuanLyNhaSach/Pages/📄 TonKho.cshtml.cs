// ✅ THÊM USING CHO IHttpContextAccessor VÀ ILogger
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
// Các using hiện tại của bạn
using DocumentFormat.OpenXml.InkML; // Bạn có chắc chắn cần using này không? Nó có vẻ không liên quan.
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens; // Bạn có chắc chắn cần using này không?

namespace QuanLyNhaSach.Pages
{
    // ✅ SỬA CONSTRUCTOR ĐỂ INJECT THÊM IHttpContextAccessor VÀ ILogger
    public class TonKhoModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<TonKhoModel> _logger; // ✅ THÊM

        public List<TonKhoRow> TonKhoList { get; set; } = [];
        public string? ErrorMessage { get; private set; } // ✅ THÊM để hiển thị lỗi trên trang

        // ✅ CẬP NHẬT CONSTRUCTOR
        public TonKhoModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<TonKhoModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor; // ✅ THÊM
            _logger = logger; // ✅ THÊM
        }

        public void OnGet()
        {
            string? connectionStringNameToUse = null;
            string? connStr = null;

            // ✅ BẮT ĐẦU SỬA: LẤY CONNECTION STRING ĐỘNG TỪ SESSION
            try
            {
                if (_httpContextAccessor.HttpContext != null && _httpContextAccessor.HttpContext.Session.IsAvailable)
                {
                    connectionStringNameToUse = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionStringName");
                }

                if (!string.IsNullOrEmpty(connectionStringNameToUse))
                {
                    connStr = _configuration.GetConnectionString(connectionStringNameToUse);
                    _logger.LogInformation("TonKhoModel is using connection string key from session: '{ConnectionStringKey}'", connectionStringNameToUse);
                }
                else
                {
                    // Fallback nếu không có trong session (ví dụ: người dùng chưa đăng nhập)
                    // Sử dụng "ReadOnlyConnection" hoặc "DefaultConnection" tùy theo cấu hình mặc định của bạn
                    connectionStringNameToUse = "ReadOnlyConnection"; // HOẶC "DefaultConnection"
                    connStr = _configuration.GetConnectionString(connectionStringNameToUse);
                    _logger.LogWarning("ActiveConnectionStringName not found in session or session not available. TonKhoModel is falling back to: '{FallbackConnectionStringKey}'", connectionStringNameToUse);
                }

                if (string.IsNullOrEmpty(connStr))
                {
                    ErrorMessage = $"Lỗi: Không tìm thấy chuỗi kết nối cho key '{connectionStringNameToUse ?? "Default/Fallback"}' trong cấu hình.";
                    _logger.LogError(ErrorMessage);
                    return; // Dừng nếu không có connection string
                }
            }
            catch (Exception ex) // Bắt lỗi khi truy cập session hoặc configuration
            {
                ErrorMessage = "Lỗi khi lấy thông tin cấu hình kết nối.";
                _logger.LogError(ex, ErrorMessage);
                return;
            }
            // ✅ KẾT THÚC SỬA: LẤY CONNECTION STRING ĐỘNG TỪ SESSION

            try
            {
                using SqlConnection conn = new(connStr);
                conn.Open();

                string query = @"
                    SELECT 
                        s.MaSach,
                        s.TieuDe, -- Đổi từ TieuDe thành TenSach nếu cột trong DB là TenSach
                        ISNULL(SUM(ctpn.SoLuong), 0) AS TongNhap,
                        ISNULL(SUM(cthd.SoLuong), 0) AS TongBan,
                        ISNULL(SUM(ctpn.SoLuong), 0) - ISNULL(SUM(cthd.SoLuong), 0) AS TonKho
                    FROM Sach s
                    LEFT JOIN ChiTietPhieuNhap ctpn ON s.MaSach = ctpn.MaSach
                    LEFT JOIN ChiTietHoaDon cthd ON s.MaSach = cthd.MaSach
                    GROUP BY s.MaSach, s.TieuDe -- Đổi TieuDe thành TenSach nếu cần
                    ORDER BY s.MaSach; -- Thêm ORDER BY để kết quả nhất quán
                ";
                // LƯU Ý: Đã sửa tên alias của bảng trong query để rõ ràng hơn (ctpn, cthd)
                // và sửa lại tên cột MaSach, TieuDe nếu cần cho khớp với bảng Sach của bạn

                using SqlCommand cmd = new(query, conn);
                using SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    TonKhoList.Add(new TonKhoRow
                    {
                        // ✅ SỬA: Sử dụng GetString cho MaSach nếu MaSach trong DB là VARCHAR/NVARCHAR
                        // Nếu MaSach là INT thì GetInt32 là đúng. Giả sử MaSach là VARCHAR(5) như các ví dụ trước.
                        MaSach = reader.GetString(reader.GetOrdinal("MaSach")), // Lấy theo tên cột cho an toàn
                        TieuDe = reader.GetString(reader.GetOrdinal("TieuDe")), // Hoặc TenSach
                        TongNhap = reader.GetInt32(reader.GetOrdinal("TongNhap")),
                        TongBan = reader.GetInt32(reader.GetOrdinal("TongBan")),
                        TonKho = reader.GetInt32(reader.GetOrdinal("TonKho"))
                    });
                }
            }
            catch (SqlException sqlEx) // Bắt lỗi SQL cụ thể
            {
                ErrorMessage = "Lỗi truy vấn cơ sở dữ liệu: " + sqlEx.Message;
                _logger.LogError(sqlEx, ErrorMessage);
            }
            catch (Exception ex) // Bắt lỗi chung khác
            {
                ErrorMessage = "Lỗi không xác định khi lấy dữ liệu tồn kho: " + ex.Message;
                _logger.LogError(ex, ErrorMessage);
            }
        }
    }

    public class TonKhoRow
    {
        // ✅ SỬA: Kiểu dữ liệu của MaSach nếu nó là string
        public string MaSach { get; set; } = string.Empty; // Nếu MaSach là VARCHAR/NVARCHAR
        // public int MaSach { get; set; } // Nếu MaSach là INT
        public string TieuDe { get; set; } = string.Empty; // Hoặc TenSach
        public int TongNhap { get; set; }
        public int TongBan { get; set; }
        public int TonKho { get; set; }
    }
}