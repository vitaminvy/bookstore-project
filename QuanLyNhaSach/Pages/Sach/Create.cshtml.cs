using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks; // Thêm async cho OnGet nếu cần, OnPost đã có

namespace QuanLyNhaSach.Pages.Sach
{
    public class CreateSachModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateSachModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        [BindProperty] public string MaSach { get; set; } = "";
        [BindProperty] public string TieuDe { get; set; } = "";
        [BindProperty] public decimal GiaBia { get; set; }
        [BindProperty] public string? MoTa { get; set; }
        [BindProperty] public int LanTaiBan { get; set; } = 1; // Mặc định là lần tái bản 1
        [BindProperty] public int? NamXuatBan { get; set; }
        [BindProperty] public string TinhTrang { get; set; } = "Còn hàng";
        [BindProperty] public string? MaNXB { get; set; }
        [BindProperty] public List<string> SelectedTheLoai { get; set; } = new();
        [BindProperty] public List<string> SelectedTacGia { get; set; } = new();

        public List<SelectListItem> NXBList { get; set; } = new();
        public List<SelectListItem> TheLoaiList { get; set; } = new();
        public List<SelectListItem> TacGiaList { get; set; } = new();
        public string? ThongBao { get; set; }

        private string? GetActiveConnectionString(bool isPostOperation = false)
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                string fallbackKey = isPostOperation ? "ManagerConnection" : "ReadOnlyConnection";
                // Đối với CreateSach, cả OnGet (load dropdown) và OnPost (thêm sách) đều có thể cần quyền của Manager
                // Hoặc OnGet có thể dùng ReadOnly nếu người dùng nào cũng có thể vào trang tạo sách để xem form
                // Nếu chắc chắn chỉ Manager vào trang này, có thể dùng ManagerConnection cho cả hai.
                // Ở đây giả định ManagerConnection cho cả hai để đơn giản.
                fallbackKey = "ReadOnlyConnection"; // Giả định chỉ Manager mới vào trang này
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        private async Task LoadSelectListsAsync(string? connStr)
        {
            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = (ThongBao ?? "") + " Lỗi: Không thể tải danh sách lựa chọn (NXB, Thể loại, Tác giả) do lỗi kết nối.";
                NXBList = new List<SelectListItem>();
                TheLoaiList = new List<SelectListItem>();
                TacGiaList = new List<SelectListItem>();
                return;
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                NXBList = new List<SelectListItem>();
                var cmd1 = new SqlCommand("SELECT MaNXB, TenNXB FROM NhaXuatBan ORDER BY TenNXB", conn);
                using (var reader1 = await cmd1.ExecuteReaderAsync())
                {
                    while (await reader1.ReadAsync())
                    {
                        NXBList.Add(new SelectListItem { Value = reader1["MaNXB"].ToString(), Text = reader1["TenNXB"].ToString() });
                    }
                }

                TheLoaiList = new List<SelectListItem>();
                var cmd2 = new SqlCommand("SELECT MaTheLoai, TenTheLoai FROM TheLoai ORDER BY TenTheLoai", conn);
                using (var reader2 = await cmd2.ExecuteReaderAsync())
                {
                    while (await reader2.ReadAsync())
                    {
                        TheLoaiList.Add(new SelectListItem { Value = reader2["MaTheLoai"].ToString(), Text = reader2["TenTheLoai"].ToString() });
                    }
                }

                TacGiaList = new List<SelectListItem>();
                var cmd3 = new SqlCommand("SELECT MaTacGia, HoTen FROM TacGia ORDER BY HoTen", conn);
                using (var reader3 = await cmd3.ExecuteReaderAsync())
                {
                    while (await reader3.ReadAsync())
                    {
                        TacGiaList.Add(new SelectListItem { Value = reader3["MaTacGia"].ToString(), Text = reader3["HoTen"].ToString() });
                    }
                }
            }
            catch (Exception ex)
            {
                ThongBao = (ThongBao ?? "") + " Lỗi khi tải danh sách lựa chọn: " + ex.Message;
                NXBList = new List<SelectListItem>();
                TheLoaiList = new List<SelectListItem>();
                TacGiaList = new List<SelectListItem>();
            }
        }


        public async Task OnGetAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý") // Chỉ Quản lý mới được vào trang tạo sách
            {
                ThongBao = "Bạn không có quyền truy cập chức năng này.";
                // Cân nhắc RedirectToPage("/AccessDenied") hoặc trang chủ
                return;
            }
            string? connStr = GetActiveConnectionString(isPostOperation: false);
            await LoadSelectListsAsync(connStr);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                ThongBao = "❌ Bạn không có quyền thực hiện chức năng này.";
                string? connStrForGet = GetActiveConnectionString(isPostOperation: false);
                await LoadSelectListsAsync(connStrForGet); // Load lại dropdowns
                return Page();
            }

            string? connStr = GetActiveConnectionString(isPostOperation: true); // Yêu cầu connection của Manager
            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = "❌ Lỗi cấu hình kết nối cho Quản lý. Không thể thêm sách.";
                await LoadSelectListsAsync(GetActiveConnectionString(false)); // Thử load lại dropdowns với quyền đọc nếu có
                return Page();
            }

            if (SelectedTheLoai.Count == 0 || SelectedTacGia.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Phải chọn ít nhất một thể loại và một tác giả.");
            }

            if (NamXuatBan.HasValue && (NamXuatBan.Value < 1000 || NamXuatBan.Value > DateTime.Now.Year + 2)) // +2 cho sách sắp XB
            {
                ModelState.AddModelError("NamXuatBan", $"Năm xuất bản phải nằm trong khoảng hợp lý (ví dụ: 1000 - {DateTime.Now.Year + 2}).");
            }


            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(connStr); // Dùng connStr của Manager vì POST operation
                return Page();
            }

            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var cmd = new SqlCommand("sp_ThemSach", connection, transaction)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@MaSach", MaSach);
                cmd.Parameters.AddWithValue("@TieuDe", TieuDe);
                cmd.Parameters.AddWithValue("@GiaBia", GiaBia);
                cmd.Parameters.AddWithValue("@MoTa", (object?)MoTa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LanTaiBan", LanTaiBan);
                cmd.Parameters.AddWithValue("@NamXuatBan", (object?)NamXuatBan ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MaNXB", string.IsNullOrEmpty(MaNXB) ? (object)DBNull.Value : MaNXB);
                cmd.Parameters.AddWithValue("@TinhTrang", TinhTrang);
                await cmd.ExecuteNonQueryAsync();

                foreach (var mtl in SelectedTheLoai)
                {
                    if (string.IsNullOrWhiteSpace(mtl)) continue;
                    var cmdTL = new SqlCommand("sp_ThemSach_TheLoai", connection, transaction)
                    {
                        CommandType = System.Data.CommandType.StoredProcedure
                    };
                    cmdTL.Parameters.AddWithValue("@MaSach", MaSach);
                    cmdTL.Parameters.AddWithValue("@MaTheLoai", mtl);
                    await cmdTL.ExecuteNonQueryAsync();
                }

                foreach (var mtg in SelectedTacGia)
                {
                    if (string.IsNullOrWhiteSpace(mtg)) continue;
                    var cmdTG = new SqlCommand("sp_ThemSach_TacGia", connection, transaction)
                    {
                        CommandType = System.Data.CommandType.StoredProcedure
                    };
                    cmdTG.Parameters.AddWithValue("@MaSach", MaSach);
                    cmdTG.Parameters.AddWithValue("@MaTacGia", mtg);
                    await cmdTG.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                ThongBao = $"✅ Thêm sách '{TieuDe}' (Mã: {MaSach}) thành công!";

                // Reset form
                ModelState.Clear();
                MaSach = string.Empty;
                TieuDe = string.Empty;
                GiaBia = 0;
                MoTa = null;
                LanTaiBan = 1;
                NamXuatBan = null;
                TinhTrang = "Còn hàng";
                MaNXB = null;
                SelectedTacGia = new List<string>();
                SelectedTheLoai = new List<string>();

            }
            catch (SqlException sqlEx)
            {
                try { transaction.Rollback(); } catch { /* Log rollback failure if necessary */ }
                if (sqlEx.Number == 2627 || sqlEx.Number == 2601) // Lỗi trùng khóa
                {
                    ThongBao = $"❌ Lỗi: Mã sách '{MaSach}' đã tồn tại.";
                    ModelState.AddModelError("MaSach", ThongBao);
                }
                else
                {
                    ThongBao = $"❌ Lỗi SQL khi thêm sách: {sqlEx.Message} (Số lỗi: {sqlEx.Number})";
                }
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch { /* Log rollback failure if necessary */ }
                ThongBao = "❌ Lỗi không mong muốn: " + ex.Message;
            }

            await LoadSelectListsAsync(connStr); // Load lại dropdowns với connection hiện tại
            return Page();
        }
    }
}