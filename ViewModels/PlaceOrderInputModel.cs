using System.ComponentModel.DataAnnotations;

namespace final.ViewModels;

public sealed class PlaceOrderInputModel : IValidatableObject
{
    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
    [StringLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "Số điện thoại")]
    [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
    [Phone(ErrorMessage = "Vui lòng nhập số điện thoại hợp lệ.")]
    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Địa chỉ nhận hàng")]
    [Required(ErrorMessage = "Địa chỉ nhận hàng là bắt buộc.")]
    [StringLength(300)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Display(Name = "Ghi chú (không bắt buộc)")]
    [StringLength(1000)]
    public string? Note { get; set; }

    public List<OrderLineInputModel> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items is null || Items.Count == 0)
        {
            yield return new ValidationResult(
                "Chọn ít nhất một sản phẩm trước khi đặt hàng.", [nameof(Items)]);
            yield break;
        }

        if (Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
        {
            yield return new ValidationResult(
                "Mỗi sản phẩm chỉ được xuất hiện một lần trong giỏ hàng.", [nameof(Items)]);
        }
    }
}
