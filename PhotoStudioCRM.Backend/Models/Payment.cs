using System.ComponentModel.DataAnnotations;

namespace PhotoStudioCRM.Backend.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public string? PaymentMethod { get; set; }

        public string? TransactionId { get; set; }

        // Navigation property
        public virtual Order? Order { get; set; }
    }
}