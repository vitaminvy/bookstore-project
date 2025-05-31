using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaSach.Pages.HoaDon // Đảm bảo namespace đúng
{
    public class ChiTietInput
    {
        [Required(ErrorMessage = "Vui lòng chọn mã sách.")]
        public string? MaSach { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
        public int SoLuong { get; set; } = 1; // Mặc định số lượng là 1
    }

    public class CreateHoaDonModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CreateHoaDonModel> _logger;

        public CreateHoaDonModel(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, ILogger<CreateHoaDonModel> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            Input = new InputModel();
            ChiTiet = new List<ChiTietInput>();
            KhachHangList = new SelectList(new List<SelectListItem>());
            KhuyenMaiList = new SelectList(new List<SelectListItem>());
            SachList = new List<SelectListItem>(); // Khởi tạo SachList
        }

        [BindProperty] public InputModel Input { get; set; }
        [BindProperty] public List<ChiTietInput> ChiTiet { get; set; }
        public SelectList KhachHangList { get; set; }
        public SelectList KhuyenMaiList { get; set; }
        public List<SelectListItem> SachList { get; set; } // Danh sách sách cho dropdown
        public string? Message { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng chọn khách hàng.")]
            public string? SoDienThoai { get; set; }
            public string? MaKhuyenMai { get; set; }
        }

        private string? GetActiveConnectionString(bool allowFallback = true)
        {
            string? connStr = null;
            string? connectionStringSource = "Session";

            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (!string.IsNullOrEmpty(connStr))
            {
                _logger.LogInformation("CreateHoaDonModel: Using ActiveConnectionString from session.");
            }
            else if (allowFallback)
            {
                string fallbackKey = "EmployeeConnection";
                connStr = _configuration.GetConnectionString(fallbackKey);
                connectionStringSource = $"Fallback ({fallbackKey})";
                _logger.LogWarning("CreateHoaDonModel: ActiveConnectionString not found in session. Falling back to '{FallbackKey}'.", fallbackKey);
            }
            else
            {
                _logger.LogWarning("CreateHoaDonModel: ActiveConnectionString not found in session and fallback is not allowed for this operation.");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogError("CreateHoaDonModel: Connection string could not be determined (Source attempted: {ConnectionSource}).", connectionStringSource);
            }
            return connStr;
        }

        private async Task LoadDropdownsAsync(string? connStr)
        {
            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogError("LoadDropdownsAsync: Không có chuỗi kết nối để tải dữ liệu.");
                Message = (Message ?? "") + " Lỗi cấu hình: Không thể tải danh sách lựa chọn.";
                KhachHangList = new SelectList(new List<SelectListItem>());
                KhuyenMaiList = new SelectList(new List<SelectListItem>());
                SachList = new List<SelectListItem>(); // Khởi tạo rỗng
                return;
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                _logger.LogInformation("LoadDropdownsAsync: Connection opened.");

                var khListInternal = new List<SelectListItem>();
                using (var khCmd = new SqlCommand("SELECT SDT, SDT + N' - ' + HoTen AS DisplayText FROM KhachHang ORDER BY HoTen", conn))
                using (var khReader = await khCmd.ExecuteReaderAsync())
                {
                    while (await khReader.ReadAsync())
                    {
                        khListInternal.Add(new SelectListItem
                        {
                            Value = khReader["SDT"].ToString(),
                            Text = khReader["DisplayText"].ToString()
                        });
                    }
                }
                KhachHangList = new SelectList(khListInternal, "Value", "Text", Input?.SoDienThoai);
                _logger.LogInformation("Đã tải {Count} khách hàng.", khListInternal.Count);

                var kmListInternal = new List<SelectListItem>();
                string kmQuery = "SELECT MaKhuyenMai, MoTa FROM KhuyenMai WHERE NgayBatDau <= GETDATE() AND NgayKetThuc >= CONVERT(date, GETDATE()) AND isDeleted=0 ORDER BY MoTa";
                using (var kmCmd = new SqlCommand(kmQuery, conn))
                using (var kmReader = await kmCmd.ExecuteReaderAsync())
                {
                    while (await kmReader.ReadAsync())
                    {
                        kmListInternal.Add(new SelectListItem
                        {
                            Value = kmReader["MaKhuyenMai"].ToString(),
                            Text = kmReader["MoTa"].ToString()
                        });
                    }
                }
                KhuyenMaiList = new SelectList(kmListInternal, "Value", "Text", Input?.MaKhuyenMai);
                _logger.LogInformation("Đã tải {Count} khuyến mãi.", kmListInternal.Count);

                // Tải danh sách sách
                var sachListInternal = new List<SelectListItem>();
                // Câu lệnh SQL này không lọc theo IsDeleted nữa
                string sachQuery = "SELECT MaSach, MaSach + N' - ' + TieuDe AS DisplayText FROM Sach ORDER BY TieuDe";
                _logger.LogInformation("Executing Sach query: {Query}", sachQuery);
                using (var sachCmd = new SqlCommand(sachQuery, conn))
                using (var sachReader = await sachCmd.ExecuteReaderAsync())
                {
                    while (await sachReader.ReadAsync())
                    {
                        sachListInternal.Add(new SelectListItem
                        {
                            Value = sachReader["MaSach"].ToString(),
                            Text = sachReader["DisplayText"].ToString()
                        });
                    }
                }
                SachList = sachListInternal; // Gán trực tiếp List<SelectListItem>
                _logger.LogInformation("Đã tải {Count} sách vào SachList.", SachList.Count);
                if (SachList.Count == 0)
                {
                    _logger.LogWarning("Không tìm thấy sách nào để hiển thị trong dropdown.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải dữ liệu cho dropdowns.");
                Message = (Message ?? "") + " Lỗi khi tải danh sách lựa chọn: " + ex.Message;
                KhachHangList = new SelectList(new List<SelectListItem>());
                KhuyenMaiList = new SelectList(new List<SelectListItem>());
                SachList = new List<SelectListItem>(); // Khởi tạo rỗng khi lỗi
                ModelState.AddModelError(string.Empty, "Không thể tải dữ liệu cho các lựa chọn.");
            }
        }

        public async Task OnGetAsync()
        {
            string? connStrForLoad = GetActiveConnectionString(allowFallback: true);
            if (string.IsNullOrEmpty(connStrForLoad))
            {
                Message = "Lỗi: Không thể tải dữ liệu cần thiết do lỗi cấu hình kết nối.";
                return;
            }
            await LoadDropdownsAsync(connStrForLoad);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            string? connStrForPost = GetActiveConnectionString(allowFallback: false);
            string? connStrForLoad = GetActiveConnectionString(allowFallback: true);

            if (string.IsNullOrEmpty(connStrForPost))
            {
                ModelState.AddModelError(string.Empty, "❌ Lỗi hệ thống: Không thể thực hiện thao tác do không xác định được kết nối phù hợp. Vui lòng đăng nhập lại.");
                if (!string.IsNullOrEmpty(connStrForLoad)) await LoadDropdownsAsync(connStrForLoad);
                return Page();
            }

            var maNV = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentUser");
            if (string.IsNullOrEmpty(maNV))
            {
                ModelState.AddModelError(string.Empty, "❌ Không xác định được nhân viên đăng nhập. Vui lòng đăng nhập lại.");
            }

            var validChiTiet = ChiTiet?.Where(ct => !string.IsNullOrWhiteSpace(ct.MaSach) && ct.SoLuong > 0).ToList() ?? new List<ChiTietInput>();
            if (!validChiTiet.Any())
            {
                // Nếu không có chi tiết nào được thêm, hoặc tất cả đều không hợp lệ, không cần báo lỗi ở ModelState
                // mà có thể báo lỗi chung nếu đây là yêu cầu (ví dụ: hóa đơn phải có ít nhất 1 sản phẩm)
                ModelState.AddModelError(string.Empty, "❌ Vui lòng thêm ít nhất một sản phẩm hợp lệ vào hóa đơn.");
            }


            if (!ModelState.IsValid)
            {
                if (!string.IsNullOrEmpty(connStrForLoad)) await LoadDropdownsAsync(connStrForLoad);
                return Page();
            }


            using var conn = new SqlConnection(connStrForPost);
            try
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_LapHoaDon", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@SoDienThoai", (object)Input.SoDienThoai ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MaNV", maNV);
                cmd.Parameters.AddWithValue("@MaKhuyenMai", string.IsNullOrEmpty(Input.MaKhuyenMai) ? (object)DBNull.Value : Input.MaKhuyenMai);

                var tvp = new DataTable();
                tvp.Columns.Add("MaSach", typeof(string));
                tvp.Columns.Add("SoLuong", typeof(int));

                foreach (var item in validChiTiet)
                {
                    tvp.Rows.Add(item.MaSach, item.SoLuong);
                }

                var chiTietParam = cmd.Parameters.AddWithValue("@ChiTiet", tvp);
                chiTietParam.SqlDbType = SqlDbType.Structured;
                chiTietParam.TypeName = "ChiTietHoaDonType";

                var outputHD = new SqlParameter("@MaHD", SqlDbType.Int) { Direction = ParameterDirection.Output };
                var outputTenKH = new SqlParameter("@TenKH", SqlDbType.NVarChar, 100) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(outputHD);
                cmd.Parameters.Add(outputTenKH);

                await cmd.ExecuteNonQueryAsync();
                Message = $"✔️ Đã lập hóa đơn (Mã HD: {outputHD.Value}) thành công cho khách hàng: {outputTenKH.Value ?? "Không có thông tin"}.";
                _logger.LogInformation("Hóa đơn {MaHD} đã được tạo cho khách hàng {TenKH} bởi NV {MaNV}", outputHD.Value, outputTenKH.Value, maNV);

                Input = new InputModel();
                ChiTiet = new List<ChiTietInput>();
                ModelState.Clear();
            }
            catch (SqlException ex)
            {
                Message = null;
                ModelState.AddModelError(string.Empty, $"❌ Lỗi SQL khi lưu hóa đơn: {ex.Message} (Số lỗi: {ex.Number})");
                _logger.LogError(ex, "Lỗi SQL khi thực thi sp_LapHoaDon. User: {MaNV}", maNV);
            }
            catch (Exception ex)
            {
                Message = null;
                ModelState.AddModelError(string.Empty, $"❌ Lỗi hệ thống khi lưu hóa đơn: {ex.Message}");
                _logger.LogError(ex, "Lỗi hệ thống khi lưu hóa đơn. User: {MaNV}", maNV);
            }

            if (!string.IsNullOrEmpty(connStrForLoad)) await LoadDropdownsAsync(connStrForLoad);
            return Page();
        }
    }
}