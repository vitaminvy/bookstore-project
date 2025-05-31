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
using System.ComponentModel.DataAnnotations; // Added for DataAnnotations

namespace QuanLyNhaSach.Pages.Sach
{
    public class EditModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EditModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public List<SelectListItem> NXBList { get; set; } = new();
        public List<(string MaSach, string TieuDe)> AllSachList { get; set; } = new();
        public bool SachDaChon { get; set; } = false;
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }


        [BindProperty(SupportsGet = true)] // To bind 'id' from route to MaSach
        [Required(ErrorMessage = "Mã sách không được để trống khi cập nhật.")]
        public string MaSach { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Tiêu đề không được để trống.")]
        [StringLength(255, ErrorMessage = "Tiêu đề không được vượt quá 255 ký tự.")]
        public string TieuDe { get; set; } = "";

        [BindProperty]
        [Required(ErrorMessage = "Giá bìa không được để trống.")]
        [Range(0.01, 10000000, ErrorMessage = "Giá bìa phải lớn hơn 0.")]
        [DataType(DataType.Currency)]
        public decimal GiaBia { get; set; }

        [BindProperty]
        [Range(1, 100, ErrorMessage = "Lần tái bản không hợp lệ.")]
        public int LanTaiBan { get; set; } = 1;

        [BindProperty]
        [Range(1000, 2200, ErrorMessage = "Năm xuất bản không hợp lệ.")] // Allow future years for upcoming books
        public int? NamXuatBan { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Tình trạng không được để trống.")]
        public string TinhTrang { get; set; } = "";

        [BindProperty]
        [StringLength(1000, ErrorMessage = "Mô tả không được vượt quá 1000 ký tự.")]
        public string? MoTa { get; set; }

        [BindProperty]
        public string? MaNXB { get; set; }


        private string? GetActiveConnectionString(bool isModificationOperation = false)
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                string fallbackKey = "ManagerConnection"; // Edit/Update Sach usually requires Manager rights
                connStr = _configuration.GetConnectionString(fallbackKey);
                if (string.IsNullOrEmpty(connStr) && isModificationOperation)
                {
                    ErrorMessage = "Lỗi cấu hình nghiêm trọng: Không thể xác định chuỗi kết nối cho thao tác này.";
                    return null;
                }
                else if (string.IsNullOrEmpty(connStr))
                {
                    ErrorMessage = "Lỗi cấu hình: Không thể tải dữ liệu.";
                    return null;
                }
            }
            return connStr;
        }

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                ErrorMessage = "Bạn không có quyền truy cập chức năng này.";
                return Page();
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: false);
            if (string.IsNullOrEmpty(connStr))
            {
                // ErrorMessage already set by GetActiveConnectionString or will be set if null
                if (string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = "Lỗi cấu hình kết nối.";
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmdNXB = new SqlCommand("SELECT MaNXB, TenNXB FROM NhaXuatBan ORDER BY TenNXB", conn);
                using (var readerNXB = await cmdNXB.ExecuteReaderAsync())
                {
                    while (await readerNXB.ReadAsync())
                    {
                        NXBList.Add(new SelectListItem
                        {
                            Value = readerNXB["MaNXB"].ToString() ?? "",
                            Text = readerNXB["TenNXB"].ToString() ?? ""
                        });
                    }
                }

                var cmdAll = new SqlCommand("SELECT MaSach, TieuDe FROM Sach WHERE (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY TieuDe", conn);
                using (var readerAll = await cmdAll.ExecuteReaderAsync())
                {
                    while (await readerAll.ReadAsync())
                    {
                        AllSachList.Add((readerAll["MaSach"].ToString() ?? "", readerAll["TieuDe"].ToString() ?? ""));
                    }
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    MaSach = id; // Assign id from route to the model's MaSach property
                    var cmd = new SqlCommand("SELECT * FROM Sach WHERE MaSach = @Ma AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);
                    cmd.Parameters.AddWithValue("@Ma", id);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        TieuDe = reader["TieuDe"].ToString() ?? "";
                        GiaBia = reader["GiaBia"] != DBNull.Value ? Convert.ToDecimal(reader["GiaBia"]) : 0;
                        LanTaiBan = reader["LanTaiBan"] != DBNull.Value ? Convert.ToInt32(reader["LanTaiBan"]) : 1;
                        NamXuatBan = reader["NamXuatBan"] != DBNull.Value ? Convert.ToInt32(reader["NamXuatBan"]) : null;
                        TinhTrang = reader["TinhTrang"].ToString() ?? "Còn hàng";
                        MoTa = reader["MoTa"]?.ToString();
                        MaNXB = reader["MaNXB"]?.ToString();
                        SachDaChon = true;
                    }
                    else
                    {
                        ErrorMessage = $"Không tìm thấy sách với mã: {id} hoặc sách đã bị ẩn.";
                        SachDaChon = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi khi tải dữ liệu: " + ex.Message;
                SachDaChon = false;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            SuccessMessage = null;
            ErrorMessage = null;

            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                ErrorMessage = "Bạn không có quyền thực hiện chức năng này.";
                await LoadRequiredListsForFormOnError();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(MaSach)) // Ensure MaSach is present for update
            {
                ModelState.AddModelError("MaSach", "Vui lòng chọn một sách từ danh sách để sửa.");
            }
            if (NamXuatBan.HasValue && (NamXuatBan.Value < 1000 || NamXuatBan.Value > DateTime.Now.Year + 5))
            {
                ModelState.AddModelError("NamXuatBan", $"Năm xuất bản không hợp lệ (1000 - {DateTime.Now.Year + 5}).");
            }


            if (!ModelState.IsValid)
            {
                await LoadRequiredListsForFormOnError();
                return Page();
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: true);
            if (string.IsNullOrEmpty(connStr))
            {
                ErrorMessage = "Lỗi cấu hình kết nối cho Quản lý. Không thể cập nhật sách.";
                await LoadRequiredListsForFormOnError();
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_SuaSach", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@MaSach", MaSach);
                cmd.Parameters.AddWithValue("@TieuDe", TieuDe);
                cmd.Parameters.AddWithValue("@GiaBia", GiaBia);
                cmd.Parameters.AddWithValue("@MoTa", (object?)MoTa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LanTaiBan", LanTaiBan);
                cmd.Parameters.AddWithValue("@NamXuatBan", (object?)NamXuatBan ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MaNXB", string.IsNullOrEmpty(MaNXB) ? (object)DBNull.Value : MaNXB);
                cmd.Parameters.AddWithValue("@TinhTrang", TinhTrang);
                // Add other parameters like @MaTheLoai, @MaTacGia if your sp_SuaSach handles them

                await cmd.ExecuteNonQueryAsync();
                SuccessMessage = $"✅ Cập nhật sách '{TieuDe}' (Mã: {MaSach}) thành công!";
                // After successful update, reload the data for the current MaSach to show updated values
                // and also reload the lists for the dropdowns.
                // The RedirectToPage will trigger OnGetAsync again which handles this.
                return RedirectToPage(new { id = MaSach, successMessage = SuccessMessage });
            }
            catch (SqlException sqlEx)
            {
                ErrorMessage = $"Lỗi SQL khi cập nhật sách: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Lỗi hệ thống khi cập nhật sách: " + ex.Message;
            }

            await LoadRequiredListsForFormOnError();
            return Page();
        }

        private async Task LoadRequiredListsForFormOnError()
        {
            string? connStrForGet = GetActiveConnectionString(isModificationOperation: false);
            if (string.IsNullOrEmpty(connStrForGet))
            {
                if (string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = "Lỗi cấu hình, không thể tải lại danh sách lựa chọn.";
                return;
            }
            try
            {
                using var conn = new SqlConnection(connStrForGet);
                await conn.OpenAsync();

                var cmdNXB = new SqlCommand("SELECT MaNXB, TenNXB FROM NhaXuatBan ORDER BY TenNXB", conn);
                using (var readerNXB = await cmdNXB.ExecuteReaderAsync())
                {
                    NXBList = new List<SelectListItem>(); // Clear before repopulating
                    while (await readerNXB.ReadAsync())
                    {
                        NXBList.Add(new SelectListItem
                        {
                            Value = readerNXB["MaNXB"].ToString() ?? "",
                            Text = readerNXB["TenNXB"].ToString() ?? ""
                        });
                    }
                }

                var cmdAll = new SqlCommand("SELECT MaSach, TieuDe FROM Sach WHERE (IsDeleted = 0 OR IsDeleted IS NULL) ORDER BY TieuDe", conn);
                using (var readerAll = await cmdAll.ExecuteReaderAsync())
                {
                    AllSachList = new List<(string MaSach, string TieuDe)>(); // Clear before repopulating
                    while (await readerAll.ReadAsync())
                    {
                        AllSachList.Add((readerAll["MaSach"].ToString() ?? "", readerAll["TieuDe"].ToString() ?? ""));
                    }
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(ErrorMessage)) ErrorMessage = "Lỗi khi tải lại danh sách lựa chọn: " + ex.Message;
            }
        }
    }
}