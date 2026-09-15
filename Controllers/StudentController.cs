using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers
{

     
      public class StudentController : Controller
      {
      

     
             public IActionResult Index()
            {
                  ViewBag.Title = "Hello word";
                  return View();
            }
          
          [HttpPost]
      
            public IActionResult Index( Student std)
            {
                 
             var data = $"FullName: {std.FullName}, Address: {std.Address}, School:{std.School}";

              ViewBag.data = data;


                  return View();

                 
            }      

           [HttpGet]
          public IActionResult Create()
            {
                  return View();
            }
          
            
          public IActionResult Create(Student student)
    {

        TempData["Message"] = "Thêm Student thành công!";

        return RedirectToAction("Index");
    }
}


      }
