using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http; // Required for IHttpContextAccessor
using System; // Required for DateTime, DBNull
using System.Collections.Generic; // Required for List<T>
using System.Threading.Tasks; // Required for Task

namespace QuanLyNhaSach.Pages.NhanVien
{
    public class LogModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LogModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public class LogEntry
        {
            public int LogID { get; set; }
            public string MaNV { get; set; } = "";
            public string HanhDong { get; set; } = "";
            public DateTime ThoiGian { get; set; }
            public string? GhiChu { get; set; }
        }

        public List<LogEntry> Logs { get; set; } = new();
        public string? ErrorMessage { get; set; }

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // Fallback for viewing logs, typically a manager's role or a privileged readonly role.
                // Assuming "ManagerConnection" has rights to view logs.
                // Or, if not found, use a "ReadOnlyConnection" if available and appropriate.
                string fallbackKey = "ReadOnlyConnection"; // Or "ReadOnlyConnection" / "DefaultConnection"
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        public async Task OnGetAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                ErrorMessage = "Bạn không có quyền truy cập chức năng này.";
                // Optionally, log this attempt if ILogger was injected.
                return;
            }

            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ErrorMessage = "Lỗi cấu hình: Không thể xác định chuỗi kết nối cơ sở dữ liệu.";
                // Optionally, log this critical error if ILogger was injected.
                return;
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("SELECT LogID, MaNV, HanhDong, ThoiGian, GhiChu FROM LogNhanVien ORDER BY ThoiGian DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    Logs.Add(new LogEntry
                    {
                        LogID = reader["LogID"] != DBNull.Value ? Convert.ToInt32(reader["LogID"]) : 0,
                        MaNV = reader["MaNV"].ToString() ?? "",
                        HanhDong = reader["HanhDong"].ToString() ?? "",
                        ThoiGian = reader["ThoiGian"] != DBNull.Value ? Convert.ToDateTime(reader["ThoiGian"]) : DateTime.MinValue,
                        GhiChu = reader["GhiChu"] != DBNull.Value ? reader["GhiChu"].ToString() : null
                    });
                }
            }
            catch (SqlException sqlEx)
            {
                ErrorMessage = $"Lỗi SQL khi tải nhật ký: {sqlEx.Message}";
                // Optionally, log sqlEx details if ILogger was injected.
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Đã có lỗi xảy ra: {ex.Message}";
                // Optionally, log ex details if ILogger was injected.
            }
        }
    }
}