using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration; // Added for IConfiguration
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic; // Added for List<T>
using System.Threading.Tasks;   // Added for Task

namespace QuanLyNhaSach.Pages.Nhap
{
    public class ImportAllTablesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        [BindProperty]
        public IFormFile? CsvFile { get; set; }

        [BindProperty]
        public string? TableName { get; set; }

        public string Message { get; set; } = string.Empty;

        public List<string> Tables { get; set; }

        public ImportAllTablesModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
            Tables = [];
        }

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // For import operations, a privileged connection is required.
                // This should typically be the "ManagerConnection" if the user is a manager.
                // If ActiveConnectionString isn't set in session for a manager, it's an issue.
                // We'll attempt to fallback to "ManagerConnection" from config directly
                // but this indicates the session wasn't set up as expected for this user.
                string fallbackKey = "ReadOnlyConnection";
                connStr = _configuration.GetConnectionString(fallbackKey);
                if (string.IsNullOrEmpty(connStr))
                {
                    // Log critical error: Connection string for manager/import is missing
                }
            }
            return connStr;
        }

        public void OnGet()
        {
            string? connStr = GetActiveConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                Message = "❌ Lỗi cấu hình: Không thể tải danh sách bảng.";
                Tables = []; // Ensure Tables is initialized even on error
                return;
            }
            Tables = GetTablesFromDatabase(connStr);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                Message = "❌ Bạn không có quyền thực hiện chức năng này.";
                string? connStrToReloadTables = GetActiveConnectionString();
                Tables = !string.IsNullOrEmpty(connStrToReloadTables) ? GetTablesFromDatabase(connStrToReloadTables) : new List<string>();
                return Page();
            }

            string? connStr = GetActiveConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                Message = "❌ Lỗi cấu hình kết nối cho Quản lý. Vui lòng đăng nhập lại hoặc liên hệ quản trị viên.";
                string? connStrToReloadTablesOnError = GetActiveConnectionString(); // Try to get any conn string to reload tables
                Tables = !string.IsNullOrEmpty(connStrToReloadTablesOnError) ? GetTablesFromDatabase(connStrToReloadTablesOnError) : new List<string>();
                return Page();
            }

            Tables = GetTablesFromDatabase(connStr);

            if (CsvFile == null || CsvFile.Length == 0)
            {
                Message = "❌ Vui lòng chọn một file CSV trước khi nhập.";
                return Page();
            }

            if (string.IsNullOrEmpty(TableName))
            {
                Message = "❌ Vui lòng chọn bảng để nhập dữ liệu.";
                return Page();
            }

            try
            {
                using var stream = CsvFile.OpenReadStream();
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    DetectDelimiter = true,
                    PrepareHeaderForMatch = args => args.Header.Trim()
                });

                using var dr = new CsvDataReader(csv);
                var table = new DataTable();
                table.Load(dr);

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                if (!await TableExistsAsync(TableName, conn))
                {
                    Message = $"❌ Bảng '{TableName}' không tồn tại trong cơ sở dữ liệu.";
                    return Page();
                }

                using var bulkCopy = new SqlBulkCopy(conn)
                {
                    DestinationTableName = TableName
                };
                await bulkCopy.WriteToServerAsync(table);

                Message = $"✅ Nhập thành công {table.Rows.Count} dòng vào bảng '{TableName}'!";
            }
            catch (Exception ex)
            {
                Message = $"❌ Đã xảy ra lỗi: {ex.Message}";
            }

            return Page();
        }

        private List<string> GetTablesFromDatabase(string? connStr)
        {
            var tableNames = new List<string>();
            if (string.IsNullOrEmpty(connStr)) return tableNames;

            try
            {
                using var conn = new SqlConnection(connStr);
                conn.Open();
                var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tableNames.Add(reader.GetString(0));
                }
            }
            catch (Exception)
            {
                // Log error if ILogger was available
                // For now, just return empty list or set a message if possible
                if (string.IsNullOrEmpty(Message)) // Avoid overwriting more specific messages
                {
                    Message = "❌ Không thể tải danh sách bảng từ cơ sở dữ liệu.";
                }
            }
            return tableNames;
        }

        private static async Task<bool> TableExistsAsync(string tableName, SqlConnection conn)
        {
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(); // Ensure connection is open if passed externally and might be closed
            }
            var cmd = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @name", conn);
            cmd.Parameters.AddWithValue("@name", tableName);
            var result = await cmd.ExecuteScalarAsync();
            return result != null && Convert.ToInt32(result) > 0;
        }
    }
}