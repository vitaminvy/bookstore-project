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
using CsvHelper; // Assumed from previous context if CSV import is still part of this file
using CsvHelper.Configuration; // Assumed
using System.Globalization; // Assumed
using System.IO; // Assumed
using System.Linq; // Assumed

namespace QuanLyNhaSach.Pages.Sach // Or QuanLyNhaSach.Pages.Nhap if it was moved
{
    public class ImportModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        // private readonly ILogger<ImportModel> _logger; // Bạn có thể thêm lại nếu cần logging

        public ImportModel(IConfiguration config, IHttpContextAccessor httpContextAccessor /*, ILogger<ImportModel> logger*/)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
            // _logger = logger;
            Books = new List<BookInput>();
            NhaCungCapList = new List<SelectListItem>();
            SachList = new List<SelectListItem>();
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp.")]
        public string? SelectedNCC { get; set; }

        [BindProperty]
        public List<BookInput> Books { get; set; }

        public List<SelectListItem> NhaCungCapList { get; set; }
        public List<SelectListItem> SachList { get; set; }
        public string? ThongBao { get; set; }

        public class BookInput
        {
            [Required(ErrorMessage = "Mã sách không được để trống.")]
            public string? MaSach { get; set; }

            [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
            public int SoLuong { get; set; }

            [Range(0.01, (double)decimal.MaxValue, ErrorMessage = "Giá nhập phải lớn hơn 0.")]
            [DataType(DataType.Currency)]
            public decimal GiaNhap { get; set; }
        }

        private string? GetActiveConnectionString(bool forWriteOperation = false)
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                string fallbackKey = (forWriteOperation ? "ReadOnlyConnection" : "ReadOnlyConnection");
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        private async Task LoadDropdownsAsync(string? connStr)
        {
            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = (ThongBao ?? "") + " Lỗi cấu hình: Không thể tải danh sách lựa chọn.";
                NhaCungCapList = new List<SelectListItem>();
                SachList = new List<SelectListItem>();
                return;
            }
            try
            {
                NhaCungCapList.Clear();
                SachList.Clear();

                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmdNCC = new SqlCommand("SELECT MaNCC, TenNCC FROM NhaCungCap ORDER BY TenNCC", conn);
                using (var readerNCC = await cmdNCC.ExecuteReaderAsync())
                {
                    while (await readerNCC.ReadAsync())
                    {
                        NhaCungCapList.Add(new SelectListItem
                        {
                            Value = readerNCC["MaNCC"].ToString(),
                            Text = readerNCC["TenNCC"].ToString()
                        });
                    }
                }

                // SỬA Ở ĐÂY: Bỏ điều kiện "WHERE (IsDeleted = 0 OR IsDeleted IS NULL)"
                var cmdSach = new SqlCommand("SELECT MaSach, TieuDe FROM Sach ORDER BY TieuDe", conn);
                using (var readerSach = await cmdSach.ExecuteReaderAsync())
                {
                    while (await readerSach.ReadAsync())
                    {
                        SachList.Add(new SelectListItem
                        {
                            Value = readerSach["MaSach"].ToString(),
                            Text = $"{readerSach["MaSach"]} - {readerSach["TieuDe"]}"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ThongBao = (ThongBao ?? "") + " Lỗi khi tải danh sách lựa chọn: " + ex.Message;
                NhaCungCapList = new List<SelectListItem>();
                SachList = new List<SelectListItem>();
            }
        }

        public async Task OnGetAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên")
            {
                ThongBao = "Bạn không có quyền truy cập chức năng này.";
                return;
            }
            string? connStr = GetActiveConnectionString(forWriteOperation: false);
            await LoadDropdownsAsync(connStr);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên")
            {
                ThongBao = "❌ Bạn không có quyền thực hiện chức năng này.";
                string? connStrForGet = GetActiveConnectionString(forWriteOperation: false);
                await LoadDropdownsAsync(connStrForGet);
                return Page();
            }

            string? connStr = GetActiveConnectionString(forWriteOperation: true);
            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = "❌ Lỗi hệ thống: Không thể kết nối đến cơ sở dữ liệu với quyền hạn phù hợp.";
                await LoadDropdownsAsync(GetActiveConnectionString(false));
                return Page();
            }

            var maNV = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser");
            if (string.IsNullOrEmpty(maNV))
            {
                ModelState.AddModelError("", "❌ Không xác định được nhân viên đăng nhập. Vui lòng đăng nhập lại.");
            }

            if (string.IsNullOrWhiteSpace(SelectedNCC))
            {
                ModelState.AddModelError("SelectedNCC", "❌ Vui lòng chọn nhà cung cấp.");
            }
            if (Books == null || !Books.Any(b => b.SoLuong > 0 && !string.IsNullOrWhiteSpace(b.MaSach)))
            {
                ModelState.AddModelError("", "❌ Vui lòng thêm ít nhất 1 sách với số lượng và mã sách hợp lệ.");
            }

            for (int i = 0; i < Books.Count; i++)
            {
                if (Books[i] != null) // Chỉ validate những dòng được submit (có thể có dòng trống nếu user xóa ở client)
                {
                    if (string.IsNullOrWhiteSpace(Books[i].MaSach))
                        ModelState.AddModelError($"Books[{i}].MaSach", "Mã sách không được để trống.");
                    if (Books[i].SoLuong <= 0)
                        ModelState.AddModelError($"Books[{i}].SoLuong", "Số lượng phải lớn hơn 0.");
                    if (Books[i].GiaNhap <= 0)
                        ModelState.AddModelError($"Books[{i}].GiaNhap", "Giá nhập phải lớn hơn 0.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync(connStr); // Load lại dropdowns với connStr đã lấy cho post (nếu có)
                return Page();
            }

            var validBooks = Books.Where(b => b != null && !string.IsNullOrWhiteSpace(b.MaSach) && b.SoLuong > 0 && b.GiaNhap > 0).ToList();
            if (!validBooks.Any())
            {
                ModelState.AddModelError("", "❌ Không có sách hợp lệ nào để nhập.");
                await LoadDropdownsAsync(connStr);
                return Page();
            }

            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var cmdPhieuNhap = new SqlCommand(@"
                    INSERT INTO PhieuNhap (NgayNhap, TongChiPhi, MaNV, MaNCC)
                    OUTPUT INSERTED.MaPhieuNhap 
                    VALUES (GETDATE(), 0, @MaNV, @MaNCC);", connection, transaction);
                cmdPhieuNhap.Parameters.AddWithValue("@MaNV", maNV);
                cmdPhieuNhap.Parameters.AddWithValue("@MaNCC", SelectedNCC);
                var maPhieuNhap = Convert.ToInt32(await cmdPhieuNhap.ExecuteScalarAsync());

                decimal tongChiPhi = 0;

                foreach (var book in validBooks)
                {
                    var cmdChiTiet = new SqlCommand(@"
                        INSERT INTO ChiTietPhieuNhap (MaPhieuNhap, MaSach, SoLuong, GiaNhap)
                        VALUES (@MaPN, @MaSach, @SoLuong, @GiaNhap)", connection, transaction);
                    cmdChiTiet.Parameters.AddWithValue("@MaPN", maPhieuNhap);
                    cmdChiTiet.Parameters.AddWithValue("@MaSach", book.MaSach);
                    cmdChiTiet.Parameters.AddWithValue("@SoLuong", book.SoLuong);
                    cmdChiTiet.Parameters.AddWithValue("@GiaNhap", book.GiaNhap);
                    await cmdChiTiet.ExecuteNonQueryAsync();

                    var cmdUpdateTon = new SqlCommand(@"
                        UPDATE Sach
                        SET SoLuongTon = ISNULL(SoLuongTon, 0) + @SoLuong
                        WHERE MaSach = @MaSach", connection, transaction);
                    cmdUpdateTon.Parameters.AddWithValue("@SoLuong", book.SoLuong);
                    cmdUpdateTon.Parameters.AddWithValue("@MaSach", book.MaSach);
                    await cmdUpdateTon.ExecuteNonQueryAsync();

                    tongChiPhi += book.GiaNhap * book.SoLuong;
                }

                var cmdUpdateTong = new SqlCommand(@"
                    UPDATE PhieuNhap
                    SET TongChiPhi = @Tong
                    WHERE MaPhieuNhap = @MaPN", connection, transaction);
                cmdUpdateTong.Parameters.AddWithValue("@Tong", tongChiPhi);
                cmdUpdateTong.Parameters.AddWithValue("@MaPN", maPhieuNhap);
                await cmdUpdateTong.ExecuteNonQueryAsync();

                transaction.Commit();
                ThongBao = $"✅ Nhập phiếu #{maPhieuNhap} với tổng chi phí {tongChiPhi:N0} VNĐ thành công!";

                ModelState.Clear();
                SelectedNCC = null;
                Books = new List<BookInput>();

            }
            catch (SqlException sqlEx)
            {
                try { transaction.Rollback(); } catch { }
                ThongBao = $"❌ Lỗi SQL khi nhập hàng: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch { }
                ThongBao = "❌ Lỗi hệ thống khi nhập hàng: " + ex.Message;
            }

            await LoadDropdownsAsync(GetActiveConnectionString(false));
            return Page();
        }
    }
}