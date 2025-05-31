using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace QuanLyNhaSach.Pages.Sach
{
    public class SearchModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SearchModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        [BindProperty(SupportsGet = true)] // SupportsGet = true if you want to search via query string on GET
        public string TuKhoa { get; set; } = string.Empty;

        public List<Sach> KetQua { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public bool SearchPerformed { get; set; } = false;


        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // Fallback for search (read operation)
                string fallbackKey = "ReadOnlyConnection"; // Or "DefaultConnection"
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        public void OnGet()
        {
            // Optional: You might want to perform a search on GET if TuKhoa is in query string
            // if (!string.IsNullOrWhiteSpace(TuKhoa))
            // {
            //     await OnPostAsync(); // Call OnPost logic, but this is a GET handler
            // }
        }

        public async Task OnPostAsync() // Changed to async Task
        {
            SearchPerformed = true; // Indicate that a search has been attempted
            KetQua = new List<Sach>(); // Clear previous results

            if (string.IsNullOrWhiteSpace(TuKhoa))
            {
                // No error message, just show no results or an empty page for empty keyword
                // Or: ErrorMessage = "Vui lòng nhập từ khóa để tìm kiếm.";
                return;
            }

            string? connStr = GetActiveConnectionString();

            if (string.IsNullOrEmpty(connStr))
            {
                ErrorMessage = "Lỗi cấu hình: Không thể xác định chuỗi kết nối cơ sở dữ liệu.";
                return;
            }

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync(); // Use async open

                using var cmd = new SqlCommand("sp_TimKiemSach_TuKhoa", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@TuKhoa", (object?)TuKhoa ?? DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync(); // Use async reader
                while (await reader.ReadAsync()) // Use async read
                {
                    KetQua.Add(new Sach
                    {
                        MaSach = reader["MaSach"]?.ToString() ?? string.Empty,
                        TieuDe = reader["TieuDe"]?.ToString() ?? string.Empty,
                        GiaBia = reader["GiaBia"] != DBNull.Value ? Convert.ToDecimal(reader["GiaBia"]) : 0m,
                        MoTa = reader["MoTa"] != DBNull.Value ? reader["MoTa"]?.ToString() ?? string.Empty : string.Empty,
                        LanTaiBan = reader["LanTaiBan"] != DBNull.Value ? Convert.ToInt32(reader["LanTaiBan"]) : null,
                        NamXuatBan = reader["NamXuatBan"] != DBNull.Value ? Convert.ToInt32(reader["NamXuatBan"]) : null,
                        MaNXB = reader["MaNXB"] != DBNull.Value ? reader["MaNXB"]?.ToString() ?? string.Empty : string.Empty,
                        TinhTrang = reader["TinhTrang"]?.ToString() ?? string.Empty,
                        SoLuongTon = reader["SoLuongTon"] != DBNull.Value ? Convert.ToInt32(reader["SoLuongTon"]) : 0,
                        TacGia = reader["TacGia"]?.ToString() ?? string.Empty,
                        TheLoai = reader["TheLoai"]?.ToString() ?? string.Empty
                    });
                }
                if (!KetQua.Any() && !string.IsNullOrWhiteSpace(TuKhoa))
                {
                    ErrorMessage = $"Không tìm thấy sách nào với từ khóa: \"{TuKhoa}\".";
                }

            }
            catch (SqlException sqlEx)
            {
                ErrorMessage = $"Lỗi SQL khi tìm kiếm: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Đã có lỗi xảy ra: {ex.Message}";
            }
        }
    }

    public class Sach // Class Sach giữ nguyên
    {
        public string MaSach { get; set; } = string.Empty;
        public string TieuDe { get; set; } = string.Empty;
        public decimal GiaBia { get; set; }
        public string MoTa { get; set; } = string.Empty;
        public int? LanTaiBan { get; set; }
        public int? NamXuatBan { get; set; }
        public string MaNXB { get; set; } = string.Empty; // Should this be nullable string?
        public string TinhTrang { get; set; } = string.Empty;
        public int SoLuongTon { get; set; }
        public string TacGia { get; set; } = string.Empty;
        public string TheLoai { get; set; } = string.Empty;
    }
}