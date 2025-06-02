using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;

namespace QuanLyNhaSach.Pages.Sach
{
    public class CreateSachModel : PageModel
    {
        private readonly string? connStr;
        private readonly IConfiguration _config;
        public CreateSachModel(IConfiguration config)
        {
            _config = config;
            connStr = _config.GetConnectionString("ManagerConnection");

        }
        [BindProperty] public string MaSach { get; set; } = "";
        [BindProperty] public string TieuDe { get; set; } = "";
        [BindProperty] public decimal GiaBia { get; set; }
        [BindProperty] public string? MoTa { get; set; }
        [BindProperty] public int LanTaiBan { get; set; } = 0;
        [BindProperty] public int? NamXuatBan { get; set; }
        [BindProperty] public string TinhTrang { get; set; } = "Còn hàng";
        [BindProperty] public string? MaNXB { get; set; }
        [BindProperty] public List<string> SelectedTheLoai { get; set; } = new();
        [BindProperty] public List<string> SelectedTacGia { get; set; } = new();

        public List<SelectListItem> NXBList { get; set; } = new();
        public List<SelectListItem> TheLoaiList { get; set; } = new();
        public List<SelectListItem> TacGiaList { get; set; } = new();
        public string? ThongBao { get; set; }

        public void OnGet()
        {
            using var conn = new SqlConnection(connStr);
            conn.Open();

            var cmd1 = new SqlCommand("SELECT MaNXB, TenNXB FROM NhaXuatBan", conn);
            using var reader1 = cmd1.ExecuteReader();
            while (reader1.Read())
            {
                NXBList.Add(new SelectListItem { Value = reader1["MaNXB"].ToString(), Text = reader1["TenNXB"].ToString() });
            }
            reader1.Close();

            var cmd2 = new SqlCommand("SELECT MaTheLoai, TenTheLoai FROM TheLoai", conn);
            using var reader2 = cmd2.ExecuteReader();
            while (reader2.Read())
            {
                TheLoaiList.Add(new SelectListItem { Value = reader2["MaTheLoai"].ToString(), Text = reader2["TenTheLoai"].ToString() });
            }
            reader2.Close();

            var cmd3 = new SqlCommand("SELECT MaTacGia, HoTen FROM TacGia", conn);
            using var reader3 = cmd3.ExecuteReader();
            while (reader3.Read())
            {
                TacGiaList.Add(new SelectListItem { Value = reader3["MaTacGia"].ToString(), Text = reader3["HoTen"].ToString() });
            }
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                OnGet();
                return Page();
            }

            
            using var conn = new SqlConnection(connStr);
            conn.Open();
            var trans = conn.BeginTransaction();

            try
            {
                // Thêm sách
                var cmd = new SqlCommand("sp_ThemSach", conn, trans)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };
                cmd.Parameters.AddWithValue("@MaSach", MaSach);
                cmd.Parameters.AddWithValue("@TieuDe", TieuDe);
                cmd.Parameters.AddWithValue("@GiaBia", GiaBia);
                cmd.Parameters.AddWithValue("@MoTa", (object?)MoTa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LanTaiBan", LanTaiBan);
                cmd.Parameters.AddWithValue("@NamXuatBan", (object?)NamXuatBan ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MaNXB", MaNXB ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@TinhTrang", TinhTrang);
                cmd.ExecuteNonQuery();

                // Gán thể loại
                foreach (var mtl in SelectedTheLoai)
                {
                    var cmdTL = new SqlCommand("sp_ThemSach_TheLoai", conn, trans)
                    {
                        CommandType = System.Data.CommandType.StoredProcedure
                    };
                    cmdTL.Parameters.AddWithValue("@MaSach", MaSach);
                    cmdTL.Parameters.AddWithValue("@MaTheLoai", mtl);
                    cmdTL.ExecuteNonQuery();
                }

                // Gán tác giả
                foreach (var mtg in SelectedTacGia)
                {
                    var cmdTG = new SqlCommand("sp_ThemSach_TacGia", conn, trans)
                    {
                        CommandType = System.Data.CommandType.StoredProcedure
                    };
                    cmdTG.Parameters.AddWithValue("@MaSach", MaSach);
                    cmdTG.Parameters.AddWithValue("@MaTacGia", mtg);
                    cmdTG.ExecuteNonQuery();
                }

                trans.Commit();
                ThongBao = "✅ Thêm sách thành công!";
            }
            catch (Exception ex)
            {
                trans.Rollback();
                ThongBao = "❌ Lỗi: " + ex.Message;
            }

            OnGet();
            return Page();
        }
    }
}


