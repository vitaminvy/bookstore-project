using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyNhaSach.Pages.TheLoai
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public IndexModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
            DanhSach = new List<TheLoai>(); // Initialize in constructor or directly
        }
        public class TheLoai
        {
            public string MaTheLoai { get; set; } = string.Empty;
            public string TenTheLoai { get; set; } = string.Empty;
            public string? MoTa { get; set; }
        }

        public List<TheLoai> DanhSach { get; set; }
        public string? ErrorMessage { get; set; }

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                string fallbackKey = "ReadOnlyConnection"; // Or "DefaultConnection"
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        public async Task OnGetAsync() // Changed to async Task
        {
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

                var cmd = new SqlCommand("SELECT MaTheLoai, TenTheLoai, MoTa FROM TheLoai ORDER BY TenTheLoai", conn);
                using var reader = await cmd.ExecuteReaderAsync(); // Use async reader

                DanhSach = new List<TheLoai>(); // Ensure list is new for each request
                while (await reader.ReadAsync()) // Use async read
                {
                    DanhSach.Add(new TheLoai
                    {
                        MaTheLoai = reader["MaTheLoai"].ToString() ?? string.Empty,
                        TenTheLoai = reader["TenTheLoai"].ToString() ?? string.Empty,
                        MoTa = reader["MoTa"] != DBNull.Value ? reader["MoTa"].ToString() : null
                    });
                }
            }
            catch (SqlException sqlEx)
            {
                ErrorMessage = $"Lỗi SQL khi tải danh sách thể loại: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Đã có lỗi xảy ra: {ex.Message}";
            }
        }
    }
}