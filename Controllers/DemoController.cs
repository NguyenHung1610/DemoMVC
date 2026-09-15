using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DemoMVC.Models;

namespace DemoMVC.Controllers;

public class DemoController : Controller
{
    // GET: /Student/Index
    public IActionResult Index()
    {
        ViewBag.FullName = "Nguyen Van An";

        ViewData["Address"] = "Ha Noi";

        return View();
    }

    // GET: /Student/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: /Student/Create
    [HttpPost]
    public IActionResult Create(Student student)
    {
        // Giả sử lưu Student vào database thành công

        TempData["Message"] = "Thêm Student thành công!";

        return RedirectToAction("Index");
    }
}