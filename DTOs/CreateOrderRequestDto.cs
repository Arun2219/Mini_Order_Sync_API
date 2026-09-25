using System.ComponentModel.DataAnnotations;

namespace Mini_Order_Sync_API.DTOs;

public class CreateOrderRequestDto
{
    [Required(ErrorMessage = "OrderNumber is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "OrderNumber must be between 1 and 50 characters.")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "CustomerName is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "CustomerName must be between 1 and 100 characters.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "CustomerEmail is required.")]
    [EmailAddress(ErrorMessage = "A valid CustomerEmail is required.")]
    [StringLength(255, ErrorMessage = "CustomerEmail cannot exceed 255 characters.")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "TotalAmount is required.")]
    [Range(0.01, 999999999999.99, ErrorMessage = "TotalAmount must be greater than 0.")]
    public decimal TotalAmount { get; set; }

    [Required(ErrorMessage = "At least one order line is required.")]
    [MinLength(1, ErrorMessage = "Order must have at least one line item.")]
    public List<CreateOrderLineDto> OrderLines { get; set; } = [];
}

public class CreateOrderLineDto
{
    [Required(ErrorMessage = "Sku is required.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Sku must be between 1 and 50 characters.")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "ProductName is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "ProductName must be between 1 and 200 characters.")]
    public string ProductName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quantity is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    [Required(ErrorMessage = "UnitPrice is required.")]
    [Range(0.01, 999999999999.99, ErrorMessage = "UnitPrice must be greater than 0.")]
    public decimal UnitPrice { get; set; }
}
