using System.ComponentModel.DataAnnotations;

namespace PhotoStudioCRM.Backend.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ReportType { get; set; } = string.Empty;

        [Required]
        public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string? Data { get; set; } // JSON data

        public int GeneratedByUserId { get; set; }

        // Navigation property
        public virtual User? GeneratedByUser { get; set; }
    }
}