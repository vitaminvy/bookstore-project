using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuanLyNhaSach.Pages.Sach
{
    public class ViewHistoryModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ViewHistoryModel(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _configuration = config;
            _httpContextAccessor = httpContextAccessor;
        }

        public List<HistoryRecord> HistoryList { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public class HistoryRecord
        {
            public int ID { get; set; }
            public string? MaSach { get; set; }
            public int SoLuong { get; set; }
            public decimal GiaNhap { get; set; }
            public DateTime NgayNhap { get; set; }
            public int MaPhieuNhap { get; set; }
            public string? MaNV { get; set; }
        }

        private string? GetActiveConnectionString()
        {
            string? connStr = null;
            if (_httpContextAccessor.HttpContext?.Session?.IsAvailable ?? false)
            {
                connStr = _httpContextAccessor.HttpContext.Session.GetString("ActiveConnectionString");
            }

            if (string.IsNullOrEmpty(connStr))
            {
                // Fallback for viewing history, typically a manager or employee task.
                // The connection string should allow reading from LichSuNhap table.
                string fallbackKey = "ReadOnlyConnection"; // Or "ManagerConnection", "ReadOnlyConnection"
                connStr = _configuration.GetConnectionString(fallbackKey);
            }
            return connStr;
        }

        public async Task OnGetAsync()
        {
            var currentRole = _httpContextAccessor.HttpContext?.Session?.GetString("CurrentRole");
            if (currentRole != "Quản lý" && currentRole != "Nhân viên")
            {
                ErrorMessage = "Bạn không có quyền truy cập chức năng này.";
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
                await conn.OpenAsync();

                var cmd = new SqlCommand("SELECT ID, MaSach, SoLuong, GiaNhap, NgayNhap, MaPhieuNhap, MaNV FROM LichSuNhap ORDER BY NgayNhap DESC", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    HistoryList.Add(new HistoryRecord
                    {
                        ID = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0,
                        MaSach = reader["MaSach"] != DBNull.Value ? reader["MaSach"].ToString() : null,
                        SoLuong = reader["SoLuong"] != DBNull.Value ? Convert.ToInt32(reader["SoLuong"]) : 0,
                        GiaNhap = reader["GiaNhap"] != DBNull.Value ? Convert.ToDecimal(reader["GiaNhap"]) : 0m,
                        NgayNhap = reader["NgayNhap"] != DBNull.Value ? Convert.ToDateTime(reader["NgayNhap"]) : DateTime.MinValue,
                        MaPhieuNhap = reader["MaPhieuNhap"] != DBNull.Value ? Convert.ToInt32(reader["MaPhieuNhap"]) : 0,
                        MaNV = reader["MaNV"] != DBNull.Value ? reader["MaNV"].ToString() : null
                    });
                }
            }
            catch (SqlException sqlEx)
            {
                ErrorMessage = $"Lỗi SQL khi tải lịch sử nhập hàng: {sqlEx.Message}";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Đã có lỗi xảy ra: {ex.Message}";
            }
        }
    }
}