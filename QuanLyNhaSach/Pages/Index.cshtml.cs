using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System; // Required for Exception

namespace QuanLyNhaSach.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor; // Added

        public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration, IHttpContextAccessor httpContextAccessor) // Added IHttpContextAccessor
        {
            _logger = logger;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor; // Added
        }

        [BindProperty]
        public string MaNV { get; set; } = "";

        [BindProperty]
        public string CCCD { get; set; } = "";

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            if (TempData.ContainsKey("Error"))
            {
                ErrorMessage = TempData["Error"]?.ToString();
            }
            if (TempData.ContainsKey("LoginSuccessMessage")) // Example if redirecting here with success
            {
                // ViewBag.SuccessMessage = TempData["LoginSuccessMessage"]?.ToString();
            }
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(MaNV) || string.IsNullOrWhiteSpace(CCCD))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                return RedirectToPage();
            }

            string? loginConnStr = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(loginConnStr))
            {
                _logger.LogError("DefaultConnection string is missing or empty in appsettings.json.");
                TempData["Error"] = "Lỗi hệ thống: Cấu hình kết nối không hợp lệ.";
                return RedirectToPage();
            }

            try
            {
                using var conn = new SqlConnection(loginConnStr);
                conn.Open();

                var cmd = new SqlCommand("SELECT ChucVu FROM NhanVien WHERE MaNV = @MaNV AND CCCD = @CCCD", conn);
                cmd.Parameters.AddWithValue("@MaNV", MaNV);
                cmd.Parameters.AddWithValue("@CCCD", CCCD); // Security: CCCD should be hashed

                var role = cmd.ExecuteScalar() as string;
                if (role == null)
                {
                    TempData["Error"] = "Thông tin đăng nhập không chính xác.";
                    return RedirectToPage();
                }

                // Login successful, now determine role-specific connection string
                string? roleSpecificConnectionStringName = null;
                string roleNormalized = role.ToLowerInvariant().Replace(" ", "");
                // Add accent removal if roles in DB might have accents
                // roleNormalized = RemoveAccents(roleNormalized); 


                if (roleNormalized == "quanly" || roleNormalized == "quảnlý")
                {
                    roleSpecificConnectionStringName = "ManagerConnection";
                }
                else if (roleNormalized == "nhanvien" || roleNormalized == "nhânviên")
                {
                    roleSpecificConnectionStringName = "EmployeeConnection";
                }
                // Add other roles if necessary, e.g., ReadOnly
                // else if (roleNormalized == "readonly")
                // {
                //     roleSpecificConnectionStringName = "ReadOnlyConnection";
                // }


                string? activeConnStr = null;
                if (!string.IsNullOrEmpty(roleSpecificConnectionStringName))
                {
                    activeConnStr = _configuration.GetConnectionString(roleSpecificConnectionStringName);
                }

                if (string.IsNullOrEmpty(activeConnStr))
                {
                    // Fallback or error if the role-specific connection string is not found
                    // This indicates a configuration issue for the determined role.
                    _logger.LogWarning("Role-specific connection string for role '{Role}' (key: '{Key}') not found. Falling back to login connection string for session.", role, roleSpecificConnectionStringName ?? "N/A");
                    activeConnStr = loginConnStr; // Fallback to the connection string used for login
                                                  // Or, more strictly, consider this an error:
                                                  // TempData["Error"] = $"Lỗi cấu hình cho vai trò: {role}.";
                                                  // return RedirectToPage();
                }

                // Lưu session
                _httpContextAccessor.HttpContext?.Session.SetString("CurrentUser", MaNV);
                _httpContextAccessor.HttpContext?.Session.SetString("CurrentRole", role);
                _httpContextAccessor.HttpContext?.Session.SetString("ActiveConnectionString", activeConnStr);
                _logger.LogInformation("User {MaNV} logged in with role {Role}. ActiveConnectionString set from key '{Key}'.", MaNV, role, roleSpecificConnectionStringName ?? "DefaultConnection (Fallback)");


                return RedirectToPage("/NhanVien/Index"); // Chuyển đến trang chính sau khi đăng nhập
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "SQL lỗi đăng nhập cho MaNV: {MaNV}", MaNV);
                TempData["Error"] = "Lỗi cơ sở dữ liệu khi đăng nhập. Vui lòng thử lại sau.";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi đăng nhập cho MaNV: {MaNV}", MaNV);
                TempData["Error"] = "Lỗi hệ thống. Vui lòng thử lại sau.";
                return RedirectToPage();
            }
        }
        // Optional: private static string RemoveAccents(string text) { ... }
    }
}