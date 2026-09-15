using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers;

public class Exam : Controller
{
    public IActionResult Index()
    {
        // 1. Sử dụng ViewBag (kiểu dynamic)
        ViewBag.ThongBaoBag = "Xin chào! Đây là dữ liệu từ ViewBag.";
        ViewBag.NamHoc = 2026;

        // 2. Sử dụng ViewData (kiểu Dictionary)
        ViewData["ThongBaoData"] = "Xin chào! Đây là dữ liệu từ ViewData.";
        ViewData["SoLuong"] = 100;

        // 3. Sử dụng TempData (truyền sang request/action khác)
        TempData["ThongBaoTemp"] = "Dữ liệu này được lưu tạm qua TempData!";

        return View();
    }

    // Action phụ để minh họa TempData vẫn tồn tại sau khi Redirect
    public IActionResult Create()
    {
        // Lấy dữ liệu từ TempData của request trước đó
        ViewBag.MessageFromTemp = TempData["ThongBaoTemp"] as string;
        return View();
    }
}
