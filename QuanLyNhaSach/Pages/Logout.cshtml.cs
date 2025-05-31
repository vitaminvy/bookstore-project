using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
// using Microsoft.AspNetCore.Http; // HttpContext.Session đã có sẵn trong PageModel

namespace QuanLyNhaSach.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            HttpContext.Session.Clear(); // Xóa tất cả các dữ liệu trong session hiện tại
            return RedirectToPage("/Login"); // Chuyển về trang đăng nhập
            // Hoặc nếu trang đăng nhập của bạn là /Index:
            // return RedirectToPage("/Index"); 
        }
    }
}