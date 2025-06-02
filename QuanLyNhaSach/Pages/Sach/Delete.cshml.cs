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
// using Microsoft.Extensions.Logging; // Bạn có thể thêm lại nếu cần logging

namespace QuanLyNhaSach.Pages.Sach
{
    public class DeleteModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        // private readonly ILogger<DeleteModel> _logger; // Bạn có thể thêm lại nếu cần

        public DeleteModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor /*, ILogger<DeleteModel> logger*/)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            // _logger = logger; // Bạn có thể thêm lại nếu cần
        }

        [BindProperty(SupportsGet = true)] // SupportsGet=true để id từ route có thể bind vào MaSach khi OnGet
        public string? MaSach { get; set; }

        // HoTen sẽ được load trong OnGet nếu có MaSach được truyền vào
        public string? HoTenSachCanXoa { get; set; }


        public List<SelectListItem> SachList { get; set; } = new();
        public string? ThongBao { get; set; } // Dùng cho thông báo chung trên trang
                                              // TempData["ThongBaoPage"] sẽ dùng cho redirect

        private string? GetActiveConnectionString(bool isModificationOperation = false)
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                string fallbackKey = isModificationOperation ? "ReadOnlyConnection" : "ReadOnlyConnection";
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        public async Task OnGetAsync(string? id) // id từ route parameter
        {
            // Lấy thông báo từ OnPost (nếu có redirect)
            if (TempData.ContainsKey("ThongBaoPage"))
            {
                ThongBao = TempData["ThongBaoPage"]?.ToString();
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: false);

            if (string.IsNullOrEmpty(connStr))
            {
                ThongBao = (ThongBao ?? "") + " Lỗi cấu hình: Không thể tải danh sách sách.";
                SachList = new List<SelectListItem>();
                return;
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                // SỬA Ở ĐÂY: Bỏ điều kiện "WHERE (IsDeleted = 0 OR IsDeleted IS NULL)"
                var cmdList = new SqlCommand("SELECT MaSach, TieuDe FROM  Sach WHERE isDeleted = 0 ORDER BY TieuDe", conn);
                using (var readerList = await cmdList.ExecuteReaderAsync())
                {
                    var tempList = new List<SelectListItem>();
                    while (await readerList.ReadAsync())
                    {
                        tempList.Add(new SelectListItem
                        {
                            Value = readerList["MaSach"].ToString(),
                            Text = $"{readerList["MaSach"]} - {readerList["TieuDe"]}"
                        });
                    }
                    SachList = tempList;
                } // readerList được đóng ở đây

                // Nếu có id truyền vào, tải thông tin sách đó để hiển thị xác nhận
                if (!string.IsNullOrEmpty(id))
                {
                    MaSach = id; // Gán vào BindProperty để form POST có thể dùng
                    // SỬA Ở ĐÂY: Bỏ điều kiện "AND (IsDeleted = 0 OR IsDeleted IS NULL)"
                    var cmdDetail = new SqlCommand("SELECT TieuDe FROM Sach WHERE MaSach = @MaSachParam AND isDeleted = 0", conn);
                    cmdDetail.Parameters.AddWithValue("@MaSachParam", id);
                    var tieuDeResult = await cmdDetail.ExecuteScalarAsync();
                    HoTenSachCanXoa = tieuDeResult?.ToString(); // Sử dụng thuộc tính HoTenSachCanXoa

                    if (HoTenSachCanXoa == null)
                    {
                        ThongBao = (ThongBao ?? "") + $" Không tìm thấy sách với Mã: {id} để hiển thị thông tin xóa.";
                    }
                }
            }
            catch (Exception ex)
            {
                ThongBao = (ThongBao ?? "") + " Lỗi khi tải dữ liệu sách: " + ex.Message;
                SachList = new List<SelectListItem>();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý")
            {
                TempData["ThongBaoPage"] = "❌ Bạn không có quyền thực hiện chức năng này.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(MaSach))
            {
                TempData["ThongBaoPage"] = "❌ Vui lòng chọn sách cần xóa.";
                // Gọi lại OnGetAsync để tải lại SachList trước khi return Page()
                // Tuy nhiên, vì OnGetAsync không trả về IActionResult, chúng ta không thể await trực tiếp ở đây
                // và sau đó return Page(). Cách tốt nhất là RedirectToPage() để OnGet được gọi lại qua HTTP GET.
                return RedirectToPage(new { id = MaSach }); // Truyền lại id nếu có để OnGet có thể load lại thông tin sách đã chọn
            }

            string? connStr = GetActiveConnectionString(isModificationOperation: true);

            if (string.IsNullOrEmpty(connStr))
            {
                TempData["ThongBaoPage"] = "❌ Lỗi hệ thống: Không thể xác thực kết nối của Quản lý. Vui lòng đăng nhập lại.";
                return RedirectToPage(new { id = MaSach });
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();

                var cmd = new SqlCommand("sp_XoaSach", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaSach", MaSach);

                await cmd.ExecuteNonQueryAsync();
                TempData["ThongBaoPage"] = $"✅ Xóa sách '{MaSach}' thành công!";
            }
            catch (SqlException sqlEx)
            {
                TempData["ThongBaoPage"] = $"❌ Lỗi SQL khi xóa sách: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                TempData["ThongBaoPage"] = $"❌ Lỗi hệ thống khi xóa sách: {ex.Message}";
            }

            return RedirectToPage(); // Tải lại trang OnGet để cập nhật danh sách và hiển thị thông báo
        }
    }
}