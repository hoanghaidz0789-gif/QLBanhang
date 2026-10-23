using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace final.ViewModels;

public sealed class AdminProductInputModel
{
    [Required, StringLength(120)]
    [Display(Name = "Tên sản phẩm")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(40)]
    [Display(Name = "Danh mục")]
    public string Category { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true)]
    [Display(Name = "Giá bán")]
    public decimal Price { get; set; }

    [Display(Name = "Ảnh sản phẩm")]
    public IFormFile? ImageFile { get; set; }
}
