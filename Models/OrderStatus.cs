using System.ComponentModel.DataAnnotations;

namespace final.Models;

public enum OrderStatus
{
    [Display(Name = "Chờ xác nhận")]
    Pending,
    [Display(Name = "Đã xác nhận")]
    Confirmed,
    [Display(Name = "Đang giao")]
    Shipping,
    [Display(Name = "Hoàn thành")]
    Completed,
    [Display(Name = "Đã hủy")]
    Cancelled
}
