using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks; // Added for async operations
using System; // Added for Exception

namespace QuanLyNhaSach.Pages.Xuat
{
    public class ExportAllTablesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ExportAllTablesModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
            TableName = string.Empty;
            ExportFormat = string.Empty;
            Tables = new List<string>(); // Initialize directly
            Message = string.Empty;
        }

        [BindProperty]
        public string TableName { get; set; }

        [BindProperty]
        public string ExportFormat { get; set; }

        public List<string> Tables { get; set; }
        public string Message { get; set; }

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // Exporting all tables is a high-privilege operation, typically for Manager.
                // If ActiveConnectionString (expected to be Manager's) is not in session, it's an error.
                // No fallback to a less privileged connection.
                string fallbackKey = "ManagerConnection"; // Attempt to use this if session is misconfigured for manager
                connStr = _configuration.GetConnectionString(fallbackKey);
                if (string.IsNullOrEmpty(connStr))
                {
                    Message = "Lỗi cấu hình nghiêm trọng: Không thể xác định chuỗi kết nối cho Quản lý.";
                }
            }
            return connStr;
        }

        public async Task OnGetAsync() // Changed to async
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                Message = "Bạn không có quyền truy cập chức năng này.";
                Tables = new List<string>();
                return;
            }

            string? connStr = GetActiveConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                // Message would be set by GetActiveConnectionString or override here
                if (string.IsNullOrEmpty(Message)) Message = "Lỗi cấu hình kết nối không thể tải danh sách bảng.";
                Tables = new List<string>();
                return;
            }
            Tables = await GetTablesFromDatabaseAsync(connStr);
        }

        public async Task<IActionResult> OnPostAsync() // Changed to async
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                Message = "❌ Bạn không có quyền thực hiện chức năng này.";
                string? connStrForGet = GetActiveConnectionString();
                Tables = string.IsNullOrEmpty(connStrForGet) ? new List<string>() : await GetTablesFromDatabaseAsync(connStrForGet);
                return Page();
            }

            string? connStr = GetActiveConnectionString();
            if (string.IsNullOrEmpty(connStr))
            {
                Message = "❌ Lỗi cấu hình kết nối cho Quản lý. Không thể xuất dữ liệu.";
                string? connStrForGet = GetActiveConnectionString(); // Try again for table list
                Tables = string.IsNullOrEmpty(connStrForGet) ? new List<string>() : await GetTablesFromDatabaseAsync(connStrForGet);
                return Page();
            }

            Tables = await GetTablesFromDatabaseAsync(connStr); // Load tables again in case of error and returning Page

            if (string.IsNullOrEmpty(TableName) || string.IsNullOrEmpty(ExportFormat))
            {
                Message = "❌ Vui lòng chọn bảng và định dạng xuất.";
                return Page();
            }

            var dt = new DataTable();
            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(); // Use async
                // Sanitize TableName to prevent SQL injection if it were directly from user input without selection.
                // Since it's from a dropdown populated by GetTablesFromDatabase, risk is lower but good to be mindful.
                var cmd = new SqlCommand($"SELECT * FROM [{TableName.Replace("]", "]]")}]", conn); // Basic sanitization for ']'
                using var reader = await cmd.ExecuteReaderAsync(); // Use async
                dt.Load(reader);
            }
            catch (SqlException sqlEx)
            {
                Message = $"❌ Lỗi SQL khi đọc dữ liệu từ bảng '{TableName}': {sqlEx.Message}";
                return Page();
            }
            catch (Exception ex)
            {
                Message = $"❌ Lỗi khi đọc dữ liệu từ bảng '{TableName}': {ex.Message}";
                return Page();
            }


            if (ExportFormat == "csv")
            {
                var sb = new StringBuilder();
                foreach (DataColumn col in dt.Columns)
                {
                    sb.Append($"\"{col.ColumnName.Replace("\"", "\"\"")}\","); // Quote and escape quotes in header
                }
                if (dt.Columns.Count > 0) sb.Length--;
                sb.AppendLine();

                foreach (DataRow row in dt.Rows)
                {
                    foreach (var item in row.ItemArray)
                    {
                        sb.Append($"\"{item?.ToString()?.Replace("\"", "\"\"") ?? ""}\","); // Quote and escape quotes in data
                    }
                    if (dt.Columns.Count > 0) sb.Length--;
                    sb.AppendLine();
                }
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                return File(bytes, "text/csv", $"{TableName}.csv");
            }
            else if (ExportFormat == "excel")
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add(dt, TableName); // XLWorkbook can take DataTable directly
                    worksheet.Columns().AdjustToContents(); // Adjust column width
                    using var stream = new MemoryStream();
                    workbook.SaveAs(stream);
                    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{TableName}.xlsx");
                }
                catch (Exception ex)
                {
                    Message = $"❌ Lỗi khi tạo file Excel: {ex.Message}";
                    return Page();
                }
            }

            Message = "❌ Định dạng không hỗ trợ.";
            return Page();
        }

        private async Task<List<string>> GetTablesFromDatabaseAsync(string connStr) // Changed to async
        {
            var tableNames = new List<string>();
            if (string.IsNullOrEmpty(connStr)) return tableNames;

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(); // Use async
                var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME", conn);
                using var reader = await cmd.ExecuteReaderAsync(); // Use async
                while (await reader.ReadAsync()) // Use async
                {
                    tableNames.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(Message)) Message = "❌ Không thể tải danh sách bảng từ cơ sở dữ liệu: " + ex.Message;
            }
            return tableNames;
        }
    }
}