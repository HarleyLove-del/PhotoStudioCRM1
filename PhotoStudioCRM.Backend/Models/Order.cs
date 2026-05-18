using System.ComponentModel.DataAnnotations;

namespace PhotoStudioCRM.Backend.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? CompletionDate { get; set; }

        [Required]
        public decimal TotalAmount { get; set; }

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public string? Notes { get; set; }

        // ========== ДОБАВЬТЕ ЭТИ ПОЛЯ ==========
        public string? PaymentMethod { get; set; }  // "card", "cash", "online", "installment"
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.NotPaid;
        // ======================================

        public int ClientId { get; set; }
        public int? PhotographerId { get; set; }

        public virtual User? Client { get; set; }
        public virtual User? Photographer { get; set; }

        public virtual ICollection<OrderService> OrderServices { get; set; } = new List<OrderService>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }

    // ДОБАВЬТЕ ЭТОТ ENUM
    public enum PaymentStatus
    {
        NotPaid = 0,      // Не оплачен
        PartiallyPaid = 1, // Частично оплачен
        Paid = 2,          // Полностью оплачен
        Refunded = 3       // Возврат
    }
}