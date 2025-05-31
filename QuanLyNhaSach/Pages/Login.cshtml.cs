using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Text; // For StringBuilder in RemoveAccents
using System.Globalization; // For CompareInfo in a more robust RemoveAccents

namespace QuanLyNhaSach.Pages
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginModel> _logger;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng chọn vai trò của bạn.")]
        public string SelectedRole { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập Mã nhân viên.")]
        public string MaNV { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập CCCD.")]
        [DataType(DataType.Password)]
        public string CCCD { get; set; } = "";

        public string? ErrorMessage { get; set; }

        public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private static string NormalizeString(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return string.Empty;

            string lowerCaseText = inputText.ToLowerInvariant();
            string unaccentedText = RemoveAccents(lowerCaseText);
            return unaccentedText.Replace(" ", "");
        }

        private static string RemoveAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            for (int i = 0; i < normalizedString.Length; i++)
            {
                char c = normalizedString[i];
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }


        public async Task<IActionResult> OnPostAsync()
        {
            ErrorMessage = null;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            string? connectionStringName = null;
            string? connStr = null;

            string normalizedSelectedRole = NormalizeString(SelectedRole);

            if (normalizedSelectedRole == "quanly")
            {
                connectionStringName = "ManagerConnection";
            }
            else if (normalizedSelectedRole == "nhanvien")
            {
                connectionStringName = "EmployeeConnection";
            }
            else
            {
                ErrorMessage = "Vai trò được chọn không hợp lệ.";
                _logger.LogWarning("Vai trò không hợp lệ được chọn từ form: {SelectedRole} (Normalized: {NormalizedSelectedRole})", SelectedRole, normalizedSelectedRole);
                return Page();
            }

            connStr = _configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogError("Không tìm thấy hoặc chuỗi kết nối '{ConnectionStringName}' không hợp lệ cho vai trò '{SelectedRole}'.", connectionStringName, SelectedRole);
                ErrorMessage = "Lỗi hệ thống: Cấu hình kết nối cho vai trò này không chính xác.";
                return Page();
            }

            _logger.LogInformation("Attempting login. MaNV: {MaNV}, Selected Role on form: {SelectedRole} (Normalized: {NormalizedSelectedRole}), Using ConnectionString Key: {ConnectionStringName}", MaNV, SelectedRole, normalizedSelectedRole, connectionStringName);

            try
            {
                using var tempConn = new SqlConnection(connStr);
                await tempConn.OpenAsync();

                var cmd = new SqlCommand("SELECT ChucVu FROM NhanVien WHERE MaNV = @maParam AND CCCD = @cccdParam", tempConn);
                cmd.Parameters.AddWithValue("@maParam", MaNV);
                cmd.Parameters.AddWithValue("@cccdParam", CCCD);

                var actualRoleFromDb = (await cmd.ExecuteScalarAsync())?.ToString();

                if (actualRoleFromDb != null)
                {
                    _logger.LogInformation("User validation successful for MaNV: {MaNV}. Role from DB: {ActualRoleFromDb}. Role selected on form: {SelectedRole}", MaNV, actualRoleFromDb, SelectedRole);

                    string normalizedActualRoleFromDb = NormalizeString(actualRoleFromDb);

                    if (normalizedActualRoleFromDb == normalizedSelectedRole)
                    {
                        HttpContext.Session.SetString("CurrentRole", actualRoleFromDb);
                        HttpContext.Session.SetString("CurrentUser", MaNV);
                        HttpContext.Session.SetString("ActiveConnectionString", connStr);
                        _logger.LogInformation("Role matched. Stored actual connection string in session for user {MaNV}.", MaNV);

                        _logger.LogInformation("Redirecting user {MaNV} with role {ActualRoleFromDb} to /Index.", MaNV, actualRoleFromDb);
                        return RedirectToPage("/Index");
                    }
                    else
                    {
                        _logger.LogWarning("Role mismatch for MaNV: {MaNV}. Selected role on form: {SelectedRole} (Normalized: {NormalizedSelectedRole}), but DB role is: {ActualRoleFromDb} (Normalized: {NormalizedActualRoleFromDb}).", MaNV, SelectedRole, normalizedSelectedRole, actualRoleFromDb, normalizedActualRoleFromDb);
                        ErrorMessage = "Vai trò bạn chọn không khớp với chức vụ của tài khoản này.";
                        return Page();
                    }
                }
                else
                {
                    _logger.LogWarning("Login failed for MaNV: {MaNV} with selected role: {SelectedRole}. Invalid MaNV or CCCD. ConnectionString Key: {ConnectionStringName}", MaNV, SelectedRole, connectionStringName);
                    ErrorMessage = "Sai Mã nhân viên hoặc CCCD (Mật khẩu).";
                    return Page();
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Lỗi SQL khi đăng nhập cho MaNV {MaNV} (Selected Role: {SelectedRole}, ConnectionString Key: {ConnectionStringName})", MaNV, SelectedRole, connectionStringName);
                ErrorMessage = "Lỗi kết nối hoặc truy vấn cơ sở dữ liệu. Vui lòng thử lại sau.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi đăng nhập cho MaNV {MaNV} (Selected Role: {SelectedRole}, ConnectionString Key: {ConnectionStringName})", MaNV, SelectedRole, connectionStringName);
                ErrorMessage = "Đã có lỗi không mong muốn xảy ra. Vui lòng thử lại sau.";
                return Page();
            }
        }
    }
}