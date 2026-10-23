using System.ComponentModel.DataAnnotations;
using final.Models;

namespace final.ViewModels;

public sealed class EditOrderInputModel
{
    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
    [StringLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại")]
    [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Địa chỉ nhận hàng")]
    [Required(ErrorMessage = "Địa chỉ nhận hàng là bắt buộc.")]
    [StringLength(300)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Display(Name = "Ghi chú")]
    [StringLength(1000)]
    public string? Note { get; set; }

    [Display(Name = "Trạng thái đơn hàng")]
    [EnumDataType(typeof(OrderStatus), ErrorMessage = "Trạng thái đơn hàng không hợp lệ.")]
    public OrderStatus Status { get; set; }
}
