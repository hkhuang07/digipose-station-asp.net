using System.ComponentModel.DataAnnotations;

namespace DigiPOSE.Models.DTOs
{
    public class CreateDraftRequest
    {
        [Required]
        public int TenantId { get; set; }
        
        [Required]
        public int ShiftId { get; set; }
        
        [Required]
        public int UserId { get; set; }
    }

    public class AddItemRequest
    {
        [Required]
        public int OrderId { get; set; }
        
        [Required]
        public int ProductId { get; set; }
        
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }
        
        // >>> [EDGE_SAFETY]: Chống add lặp mặt hàng nếu máy quét barcode bị nảy liên tục trong 50ms
        [Required]
        public string ClientScanId { get; set; } = Guid.NewGuid().ToString();
    }

    public class RemoveItemRequest
    {
        [Required]
        public int OrderId { get; set; }
        
        [Required]
        public int ProductId { get; set; }
    }

    public class CheckoutRequest
    {
        [Required]
        public int OrderId { get; set; }
        
        [Required]
        public int PaymentMethodId { get; set; }
        
        public int? CustomerId { get; set; }

        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Tendered amount must be non-negative")]
        public decimal TenderedAmount { get; set; } = 0;

        [Range(0, int.MaxValue, ErrorMessage = "Redeemed points must be non-negative")]
        public int RedeemedPoints { get; set; } = 0;

        // >>> [CRITICAL_IDEMPOTENCY]: Khóa duy nhất từ máy thu ngân.
        [Required]
        public Guid IdempotencyKey { get; set; }

        // >>> [ENTERPRISE RETAIL DOCUMENT CLASSIFICATION]: Standardized domain trade document specifications
        public string DocType { get; set; } = "POS_RETAIL"; // e.g., "POS_RETAIL" (B2C), "B2B_INVOICE" (Corporate)
        public int? CounterId { get; set; }
        public int? WarehouseId { get; set; }
        public string? BuyerTaxCode { get; set; }
        public string? BuyerLegalName { get; set; }
        public string? BuyerPhone { get; set; }
        public string? BuyerCccd { get; set; }
        public string? BuyerAddress { get; set; }
        public string? BuyerEmail { get; set; }
        public string? Notes { get; set; }
        public bool IsB2B { get; set; } = false;
        public string? CompanyName { get; set; }
        public string? BudgetCode { get; set; }
        public string? BankAccount { get; set; }
    }

    // >>> [HIGH_EFFECT_UI_DTO]: Response gửi trả khi Checkout xong, cung cấp đầy đủ mã số chứng từ Retail và cập nhật tồn kho tức thì!
    public record CheckoutResponseDto(
        int OrderId,
        int RetailId,
        string InvoiceNumber,
        string DocNo,
        string DocType,
        DateTime ProcessedAt,
        bool IsReplay, // True nếu đây là phản hồi lặp lại do Retry (đã chốt trước đó)
        Dictionary<int, int> LiveStockBalances,
        decimal TenderedAmount = 0,
        decimal ChangeAmount = 0
    );

    public class StartShiftRequest
    {
        [Required] public int UserId { get; set; }
        [Required] public int TenantId { get; set; }
        [Required] public int CounterId { get; set; }
        [Range(0, (double)decimal.MaxValue)] public decimal StartCash { get; set; } = 0;
    }

    public class CloseShiftRequest
    {
        [Required] public int ShiftId { get; set; }
        [Range(0, (double)decimal.MaxValue)] public decimal EndCash { get; set; } = 0;
    }

    public class CreateCustomerRequest
    {
        [Required] public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int? CustomerTypeId { get; set; }
    }

    public class AddPointsRequest
    {
        [Required] [Range(1, int.MaxValue)] public int Points { get; set; }
    }

    public class UpdateItemNoteRequest
    {
        [Required] public int OrderId { get; set; }
        [Required] public int ProductId { get; set; }
        public string? Notes { get; set; }
    }
}
