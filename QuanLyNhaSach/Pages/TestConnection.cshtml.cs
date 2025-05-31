using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http; // Added for IHttpContextAccessor
using System; // Required for Exception

namespace QuanLyNhaSach.Pages
{
    public class TestConnectionModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor; // Added

        public TestConnectionModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor) // Added IHttpContextAccessor
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor; // Added
        }

        public string ConnectionResult { get; set; } = "";
        public string UsedConnectionStringKey { get; set; } = ""; // Added to show which key was used

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            string? connStrName = null;

            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
                if (!string.IsNullOrEmpty(connStr))
                {
                    // If the full string is in session, we might not know its original key name easily.
                    // If "ActiveConnectionStringName" was stored, we'd use that.
                    // For simplicity here, if full string is in session, we'll indicate that.
                    UsedConnectionStringKey = "ActiveConnectionString (từ Session)";
                    return connStr;
                }
            }

            // Fallback if not in session or if session stores the key name instead of full string
            // For a test page, falling back to "DefaultConnection" or a specific test connection is fine.
            connStrName = "DefaultConnection"; // Or "ReadOnlyConnection", or a specific test connection string key
            UsedConnectionStringKey = $"{connStrName} (từ appsettings.json - Fallback)";
            connStr = _configuration.GetConnectionString(connStrName);

            return connStr;
        }

        public void OnGet()
        {
            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ConnectionResult = $"❌ Lỗi: Không thể xác định chuỗi kết nối. Key đã thử: {UsedConnectionStringKey}.";
                return;
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                ConnectionResult = $"✅ Đã kết nối thành công đến SQL Server! (Sử dụng key: {UsedConnectionStringKey})";
            }
            catch (Exception ex)
            {
                ConnectionResult = $"❌ Lỗi kết nối (sử dụng key: {UsedConnectionStringKey}): {ex.Message}";
            }
        }
    }
}