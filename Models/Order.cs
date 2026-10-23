using System.ComponentModel.DataAnnotations;

namespace final.Models;

public sealed class Order
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public Guid ConfirmationToken { get; set; }

    public decimal TotalAmount { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
