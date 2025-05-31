using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace QuanLyNhaSach.Pages.NhanVien
{
    public class EditNhanVienModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<EditNhanVienModel> _logger;

        public EditNhanVienModel(IConfiguration config, IHttpContextAccessor httpContextAccessor, ILogger<EditNhanVienModel> logger)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            AllNhanVienSelectList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "-- Chọn nhân viên --" } };
        }

        public List<SelectListItem> AllNhanVienSelectList { get; set; }
        public bool NhanVienDaChon { get; set; } = false;
        public string? PageErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? MaNV { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        public string HoTen { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Chức vụ không được để trống.")]
        [StringLength(50, ErrorMessage = "Chức vụ không được vượt quá 50 ký tự.")]
        public string? ChucVu { get; set; } = "";

        [BindProperty]
        [DataType(DataType.Date)]
        public DateTime? NgayVaoLam { get; set; }

        [BindProperty]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(15, ErrorMessage = "Số điện thoại không được vượt quá 15 ký tự.")]
        public string? SDT { get; set; } = "";

        [BindProperty]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
        public string? Email { get; set; } = "";

        [BindProperty]
        [StringLength(12, MinimumLength = 9, ErrorMessage = "CCCD phải có từ 9 đến 12 ký tự.")]
        public string? CCCD { get; set; } = "";

        private string? GetActiveConnectionString(bool isModificationOperation = false)
        {
            string? connStr = null;
            string? connStrSource = "Unknown";
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
                if (!string.IsNullOrEmpty(connStr))
                {
                    connStrSource = "Session (ActiveConnectionString)";
                }
            }

            if (string.IsNullOrEmpty(connStr))
            {
                string fallbackKey = "ManagerConnection";
                connStr = _configuration.GetConnectionString(fallbackKey);
                connStrSource = $"Fallback ({fallbackKey})";
                if (string.IsNullOrEmpty(connStr))
                {
                    _logger.LogError("CONNECTION STRING ERROR: Cả Session và Fallback key '{FallbackKey}' đều không cung cấp được chuỗi kết nối.", fallbackKey);
                    PageErrorMessage = "Lỗi cấu hình kết nối nghiêm trọng.";
                    return null;
                }
                _logger.LogWarning("EditNhanVienModel: Sử dụng chuỗi kết nối fallback: {FallbackKey}", fallbackKey);
            }
            _logger.LogInformation("EditNhanVienModel: Sử dụng chuỗi kết nối từ: {ConnStrSource}", connStrSource);
            return connStr;
        }

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            AllNhanVienSelectList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "-- Chọn nhân viên --" } };
            if (TempData["SuccessMessage"] != null) SuccessMessage = TempData["SuccessMessage"]?.ToString();
            if (TempData["ErrorMessage"] != null) PageErrorMessage = TempData["ErrorMessage"]?.ToString();

            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                PageErrorMessage = "Bạn không có quyền truy cập chức năng này.";
                _logger.LogWarning("User với vai trò '{CurrentRole}' đã cố gắng truy cập EditNhanVien (GET). Từ chối.", currentRole ?? "Chưa đăng nhập");
                return Page();
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: false);
            if (string.IsNullOrEmpty(connStr))
            {
                if (string.IsNullOrEmpty(PageErrorMessage)) PageErrorMessage = "Lỗi cấu hình kết nối, không thể tải dữ liệu.";
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                _logger.LogInformation("OnGetAsync: Connection opened to database for MaNV dropdown.");

                var cmdAll = new SqlCommand("SELECT MaNV, HoTen FROM NhanVien ORDER BY HoTen", conn);
                using (var readerAll = await cmdAll.ExecuteReaderAsync())
                {
                    int count = 0;
                    while (await readerAll.ReadAsync())
                    {
                        AllNhanVienSelectList.Add(new SelectListItem
                        {
                            Value = readerAll["MaNV"].ToString() ?? "",
                            Text = $"{readerAll["MaNV"].ToString() ?? ""} - {readerAll["HoTen"]?.ToString() ?? ""}"
                        });
                        count++;
                    }
                    _logger.LogInformation("OnGetAsync: Đã tải {Count} nhân viên vào AllNhanVienSelectList.", count);
                    if (count == 0) _logger.LogWarning("OnGetAsync: Không tìm thấy nhân viên nào trong CSDL để hiển thị trong dropdown.");
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    this.MaNV = id;
                }


                if (!string.IsNullOrWhiteSpace(this.MaNV))
                {
                    _logger.LogInformation("OnGetAsync: Chuẩn bị tải chi tiết cho MaNV: {MaNVFromModel}", this.MaNV);
                    if (conn.State != ConnectionState.Open)
                    {
                        _logger.LogWarning("OnGetAsync: Kết nối bị đóng trước khi đọc chi tiết NV. Mở lại...");
                        await conn.OpenAsync();
                    }

                    var cmdDetail = new SqlCommand("SELECT MaNV, HoTen, ChucVu, NgayVaoLam, SDT, Email, CCCD FROM NhanVien WHERE MaNV = @MaNVParam", conn);
                    cmdDetail.Parameters.AddWithValue("@MaNVParam", this.MaNV);

                    _logger.LogInformation("OnGetAsync: Thực thi cmdDetail cho MaNV: {MaNVFromModel}", this.MaNV);
                    using (var readerDetail = await cmdDetail.ExecuteReaderAsync())
                    {
                        _logger.LogInformation("OnGetAsync: cmdDetail.ExecuteReaderAsync() hoàn tất cho MaNV: {MaNVFromModel}. HasRows: {HasRows}", this.MaNV, readerDetail.HasRows);
                        if (await readerDetail.ReadAsync())
                        {
                            _logger.LogInformation("OnGetAsync: Đang đọc dữ liệu chi tiết cho MaNV: {MaNVFromModel}", this.MaNV);
                            HoTen = readerDetail["HoTen"].ToString() ?? "";
                            ChucVu = readerDetail["ChucVu"]?.ToString() ?? "";
                            NgayVaoLam = readerDetail["NgayVaoLam"] != DBNull.Value ? Convert.ToDateTime(readerDetail["NgayVaoLam"]) : null;
                            SDT = readerDetail["SDT"]?.ToString() ?? "";
                            Email = readerDetail["Email"]?.ToString() ?? "";
                            CCCD = readerDetail["CCCD"]?.ToString() ?? "";
                            NhanVienDaChon = true;
                            _logger.LogInformation("OnGetAsync: Đã tải xong chi tiết cho MaNV: {MaNVFromModel}", this.MaNV);
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(this.MaNV)) // Chỉ hiển thị lỗi nếu MaNV được truyền vào mà không tìm thấy
                            {
                                PageErrorMessage = $"Không tìm thấy nhân viên với mã: {this.MaNV}";
                            }
                            NhanVienDaChon = false;
                            _logger.LogWarning("OnGetAsync: Không tìm thấy dòng nào cho MaNV: {MaNVFromModel} sau khi ExecuteReader.", this.MaNV);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PageErrorMessage = "Lỗi khi tải dữ liệu nhân viên: " + ex.Message;
                _logger.LogError(ex, "Lỗi trong OnGetAsync của EditNhanVienModel khi xử lý MaNV: {MaNV}", this.MaNV ?? id ?? "NULL");
                AllNhanVienSelectList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "-- Lỗi tải danh sách --" } };
                NhanVienDaChon = false;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            SuccessMessage = null;
            PageErrorMessage = null;

            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                PageErrorMessage = "Bạn không có quyền thực hiện chức năng này.";
                await ReloadDataForForm();
                return Page();
            }

            // Đảm bảo MaNV không rỗng trước khi cố gắng cập nhật
            if (string.IsNullOrWhiteSpace(MaNV))
            {
                ModelState.AddModelError("MaNV", "Vui lòng chọn một nhân viên từ danh sách để sửa.");
            }
            if (NgayVaoLam.HasValue && NgayVaoLam.Value.Date > DateTime.Today.Date)
            {
                ModelState.AddModelError("NgayVaoLam", "Ngày vào làm không được lớn hơn ngày hiện tại.");
            }


            if (!ModelState.IsValid)
            {
                await ReloadDataForForm();
                NhanVienDaChon = true; // Giả định vẫn đang sửa nhân viên đã chọn
                return Page();
            }


            string? connStr = GetActiveConnectionString(isModificationOperation: true);
            if (string.IsNullOrEmpty(connStr))
            {
                PageErrorMessage = "Lỗi cấu hình kết nối cho Quản lý. Không thể cập nhật nhân viên.";
                await ReloadDataForForm();
                NhanVienDaChon = true;
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_SuaNhanVien", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@MaNV", MaNV);
                cmd.Parameters.AddWithValue("@HoTen", HoTen);
                cmd.Parameters.AddWithValue("@ChucVu", (object?)ChucVu ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NgayVaoLam", (object?)NgayVaoLam ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SDT", (object?)SDT ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Email", (object?)Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CCCD", (object?)CCCD ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                TempData["SuccessMessage"] = $"✅ Cập nhật nhân viên '{HoTen}' (Mã: {MaNV}) thành công!";
                _logger.LogInformation("Cập nhật thành công MaNV: {MaNV}", MaNV);

                return RedirectToPage(new { id = this.MaNV });
            }
            catch (SqlException sqlEx)
            {
                PageErrorMessage = $"Lỗi SQL khi cập nhật nhân viên: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
                _logger.LogError(sqlEx, "Lỗi SQL khi cập nhật MaNV: {MaNV}", MaNV);
            }
            catch (Exception ex)
            {
                PageErrorMessage = "Lỗi hệ thống khi cập nhật nhân viên: " + ex.Message;
                _logger.LogError(ex, "Lỗi hệ thống khi cập nhật MaNV: {MaNV}", MaNV);
            }

            NhanVienDaChon = true;
            await ReloadDataForForm();
            return Page();
        }

        private async Task ReloadDataForForm()
        {
            string? connStrForGet = GetActiveConnectionString(isModificationOperation: false);
            if (string.IsNullOrEmpty(connStrForGet))
            {
                if (string.IsNullOrEmpty(PageErrorMessage)) PageErrorMessage = "Lỗi cấu hình, không thể tải lại danh sách lựa chọn.";
                AllNhanVienSelectList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "-- Lỗi tải --" } };
                return;
            }
            try
            {
                using var conn = new SqlConnection(connStrForGet);
                await conn.OpenAsync();

                var cmdAll = new SqlCommand("SELECT MaNV, HoTen FROM NhanVien ORDER BY HoTen", conn);
                using (var readerAll = await cmdAll.ExecuteReaderAsync())
                {
                    AllNhanVienSelectList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "-- Chọn nhân viên --" } };
                    while (await readerAll.ReadAsync())
                    {
                        AllNhanVienSelectList.Add(new SelectListItem
                        {
                            Value = readerAll["MaNV"].ToString() ?? "",
                            Text = $"{readerAll["MaNV"]} - {readerAll["HoTen"]?.ToString() ?? ""}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(PageErrorMessage)) PageErrorMessage = "Lỗi khi tải lại danh sách lựa chọn nhân viên: " + ex.Message;
                _logger.LogError(ex, "Lỗi trong ReloadDataForForm của EditNhanVienModel.");
                AllNhanVienSelectList = new List<SelectListItem> { new SelectListItem { Value = "", Text = "-- Lỗi tải --" } };
            }
        }
    }
}