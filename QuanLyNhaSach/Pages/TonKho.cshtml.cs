using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace QuanLyNhaSach.Pages
{
    public class TonKhoModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TonKhoModel> _logger;

        public List<TonKhoRow> TonKhoList { get; set; } = new();
        public string? ErrorMessage { get; private set; }

        public TonKhoModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<TonKhoModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public void OnGet()
        {
            string? connectionStringName = null;
            string? connStr = null;

            try
            {
                // Lấy connection string động từ session
                if (_httpContextAccessor.HttpContext?.Session?.IsAvailable == true)
                {
                    connectionStringName = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionStringName");
                }

                if (!string.IsNullOrEmpty(connectionStringName))
                {
                    connStr = _configuration.GetConnectionString(connectionStringName);
                    _logger.LogInformation("TonKhoModel is using connection string key from session: '{ConnectionStringKey}'", connectionStringName);
                }
                else
                {
                    // Fallback nếu không có session
                    connectionStringName = "ReadOnlyConnection"; // hoặc "DefaultConnection"
                    connStr = _configuration.GetConnectionString(connectionStringName);
                    _logger.LogWarning("ActiveConnectionStringName not found in session or session not available. Using fallback: '{FallbackConnectionStringKey}'", connectionStringName);
                }

                if (string.IsNullOrEmpty(connStr))
                {
                    ErrorMessage = $"Không tìm thấy chuỗi kết nối cho key '{connectionStringName ?? "Default/Fallback"}' trong cấu hình.";
                    _logger.LogError(ErrorMessage);
                    return;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi khi lấy thông tin cấu hình kết nối.";
                _logger.LogError(ex, ErrorMessage);
                return;
            }

            // Truy vấn dữ liệu tồn kho từ bảng Sach
            try
            {
                using SqlConnection conn = new(connStr);
                conn.Open();

                string query = @"
                    SELECT MaSach, TieuDe, SoLuongTon
                    FROM Sach
                    WHERE IsDeleted = 0
                    ORDER BY MaSach
                ";
                using SqlCommand cmd = new(query, conn);
                using SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                   TonKhoList.Add(new TonKhoRow
                    {
                        MaSach = reader.IsDBNull(reader.GetOrdinal("MaSach")) ? "" : reader.GetString(reader.GetOrdinal("MaSach")),
                        TieuDe = reader.IsDBNull(reader.GetOrdinal("TieuDe")) ? "" : reader.GetString(reader.GetOrdinal("TieuDe")),
                        SoLuongTon = reader.IsDBNull(reader.GetOrdinal("SoLuongTon")) ? 0 : reader.GetInt32(reader.GetOrdinal("SoLuongTon"))
                    });
                }
            }
            catch (SqlException sqlEx)
            {
                ErrorMessage = "Lỗi truy vấn cơ sở dữ liệu: " + sqlEx.Message;
                _logger.LogError(sqlEx, ErrorMessage);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi không xác định khi lấy dữ liệu tồn kho: " + ex.Message;
                _logger.LogError(ex, ErrorMessage);
            }
        }
    }

    public class TonKhoRow
    {
        public string MaSach { get; set; } = string.Empty;
        public string TieuDe { get; set; } = string.Empty;
        public int SoLuongTon { get; set; }
    }
}
