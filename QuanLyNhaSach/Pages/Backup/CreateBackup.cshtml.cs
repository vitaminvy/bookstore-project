// ✅ THÊM CÁC USING CẦN THIẾT
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
// Các using hiện tại của bạn
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace QuanLyNhaSach.Pages.Backup
{
    public class CreateBackupModel : PageModel
    {
        // ✅ BỎ private readonly string? connStr;
        private readonly IConfiguration _configuration; // ✅ ĐỔI _config THÀNH _configuration CHO NHẤT QUÁN
        private readonly IHttpContextAccessor _httpContextAccessor; // ✅ THÊM
        private readonly ILogger<CreateBackupModel> _logger; // ✅ THÊM

        // ✅ CẬP NHẬT CONSTRUCTOR
        public CreateBackupModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<CreateBackupModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            // ✅ BỎ connStr = _config.GetConnectionString("DefaultConnection");
        }

        [BindProperty]
        public string? ThongBao { get; set; }

        public void OnGet()
        {
            // Có thể kiểm tra quyền ở đây nếu muốn và đặt ThongBao
            // var currentRole = _httpContextAccessor.HttpContext?.Session.GetString("CurrentRole");
            // if (currentRole != "Quản lý")
            // {
            //     ThongBao = "Bạn không có quyền truy cập chức năng này.";
            // }
        }

        public IActionResult OnPost()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                ThongBao = "❌ Bạn không có quyền thực hiện chức năng này.";
                _logger.LogWarning("User không phải Quản lý (Role: {UserRole}) đã cố gắng tạo backup.", currentRole ?? "Chưa đăng nhập");
                return Page();
            }

            // ✅ BẮT ĐẦU SỬA: LẤY CONNECTION STRING ĐỘNG TỪ SESSION
            string? connStr = null;
            try
            {
                if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
                {
                    // Đối với chức năng backup của "Quản lý", chúng ta kỳ vọng "ActiveConnectionString"
                    // đã được thiết lập là connection string của Quản lý (ví dụ: ManagerConnection) khi họ đăng nhập.
                    connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
                }

                if (string.IsNullOrEmpty(connStr))
                {
                    ThongBao = "❌ Lỗi: Không thể lấy chuỗi kết nối phù hợp cho Quản lý từ Session. Hãy đảm bảo bạn đã đăng nhập đúng cách.";
                    _logger.LogError("CreateBackup: ActiveConnectionString không có trong session cho người dùng Quản lý.");
                    return Page();
                }
                _logger.LogInformation("CreateBackup: Sử dụng connection string từ session của Quản lý.");
            }
            catch (Exception ex)
            {
                ThongBao = "❌ Lỗi khi truy cập thông tin Session để lấy chuỗi kết nối.";
                _logger.LogError(ex, "CreateBackup: Lỗi khi truy cập Session.");
                return Page();
            }
            // ✅ KẾT THÚC SỬA: LẤY CONNECTION STRING ĐỘNG TỪ SESSION

            try
            {
                var builder = new SqlConnectionStringBuilder(connStr);
                string databaseName = builder.InitialCatalog;

                if (string.IsNullOrEmpty(databaseName))
                {
                    ThongBao = "❌ Lỗi: Không thể xác định tên cơ sở dữ liệu từ chuỗi kết nối của Quản lý.";
                    _logger.LogError("CreateBackup: Không thể lấy databaseName từ connection string của Quản lý.");
                    return Page();
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"{databaseName}_Backup_{timestamp}.bak";

                // ✅ Cân nhắc đường dẫn backup an toàn và có quyền ghi bởi user của SQL Server service
                // Directory.GetCurrentDirectory() có thể không phải là nơi SQL Server có quyền ghi.
                // Bạn có thể cần cấu hình một đường dẫn cụ thể mà SQL Server service account có quyền.
                // Ví dụ: string backupDirectory = _configuration["BackupSettings:DirectoryPath"];
                // Nếu không có cấu hình, hãy đảm bảo thư mục này có quyền ghi phù hợp.
                string backupDirectory = Path.Combine(Directory.GetCurrentDirectory(), "DatabaseBackups");

                // Kiểm tra và tạo thư mục nếu chưa có (ứng dụng web cần quyền ghi vào đây)
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                    _logger.LogInformation("Đã tạo thư mục backup: {BackupDirectory}", backupDirectory);
                }
                string backupFilePath = Path.Combine(backupDirectory, backupFileName);

                _logger.LogInformation("Bắt đầu sao lưu CSDL: {DatabaseName} vào file: {BackupFilePath} bằng tài khoản của Quản lý.", databaseName, backupFilePath);

                using (var connection = new SqlConnection(connStr)) // ✅ Đổi tên biến conn thành connection để tránh trùng
                {
                    connection.Open();
                    // Đảm bảo tài khoản trong connStr (ví dụ: lg_manager) có quyền BACKUP DATABASE
                    string backupCommandText = $"BACKUP DATABASE [{databaseName}] TO DISK = N'{backupFilePath}' WITH NOFORMAT, NOINIT, NAME = N'{databaseName}-Full Database Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";

                    _logger.LogInformation("Executing backup command: {BackupCommand}", backupCommandText);

                    using (var cmd = new SqlCommand(backupCommandText, connection))
                    {
                        cmd.CommandTimeout = 300; // ✅ Tăng thời gian timeout cho lệnh backup (ví dụ 5 phút)
                        cmd.ExecuteNonQuery();
                    }
                }
                ThongBao = $"✅ Sao lưu cơ sở dữ liệu '{databaseName}' thành công!\nTệp được lưu tại: {backupFilePath}";
                _logger.LogInformation("Sao lưu CSDL {DatabaseName} thành công.", databaseName);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi SQL khi sao lưu CSDL. Error Number: {ErrorNumber}", ex.Number);
                ThongBao = $"❌ Lỗi SQL khi sao lưu cơ sở dữ liệu: {ex.Message} (Mã lỗi: {ex.Number}).\n" +
                           $"Hãy kiểm tra quyền hạn của tài khoản SQL ({new SqlConnectionStringBuilder(connStr).UserID}) " +
                           $"và đường dẫn lưu file backup có hợp lệ và có quyền ghi bởi SQL Server service không.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không mong muốn khi sao lưu CSDL.");
                ThongBao = $"❌ Đã xảy ra lỗi không mong muốn trong quá trình sao lưu: {ex.Message}.";
            }
            return Page();
        }
    }
}