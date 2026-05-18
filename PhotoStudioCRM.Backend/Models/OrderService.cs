using System.ComponentModel.DataAnnotations;

namespace PhotoStudioCRM.Backend.Models
{
    public class OrderService
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }
        public int ServiceId { get; set; }

        public decimal PriceAtTime { get; set; }

        // Navigation properties
        public virtual Order? Order { get; set; }
        public virtual Service? Service { get; set; }
    }
}