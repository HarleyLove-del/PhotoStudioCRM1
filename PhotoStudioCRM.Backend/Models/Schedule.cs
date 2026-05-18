using System.ComponentModel.DataAnnotations;

namespace PhotoStudioCRM.Backend.Models
{
    public class Schedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PhotographerId { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public int? OrderId { get; set; }

        public bool IsAvailable { get; set; } = true;

        // Navigation properties
        public virtual User? Photographer { get; set; }
        public virtual Order? Order { get; set; }
    }
}