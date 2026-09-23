using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace DemoMVC.Models
{
      [Table("Students")]
  public class Student
      {
             [Key]
            public string? FullName {get; set; }
             public string? Address {get; set; }

             public string? School {get; set; }
             
              public string? Email {get; set; } = default!;
      } 


}