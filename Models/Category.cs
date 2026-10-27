using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [Display(Name = "Tên danh mục")]
        public string ?CategoryName { get; set; }

        // Quan hệ 1 - Nhiều với Product
        public ICollection<Product>? Products { get; set; }
    }
}