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

           
          public IActionResult Create()
            {
                  return View();
            }

      }
}