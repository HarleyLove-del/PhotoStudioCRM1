using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace PhotoStudioCRM.Backend.Models
{
    public class Service
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        // ========== НОВЫЕ ПОЛЯ ДЛЯ УЛУЧШЕННОГО ОТОБРАЖЕНИЯ ==========

        /// <summary>
        /// Ссылка на изображение услуги
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Иконка для отображения (эмодзи или ссылка)
        /// </summary>
        public string? Icon { get; set; }

        /// <summary>
        /// Категория услуги (photo, video, edit, studio)
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Дополнительные опции/эффекты для услуги (хранятся как JSON)
        /// </summary>
        public string? ExtraOptionsJson { get; set; }

        /// <summary>
        /// Несериализованное свойство для доступа к дополнительным опциям
        /// </summary>
        public List<ServiceExtraOption>? ExtraOptions
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExtraOptionsJson))
                    return new List<ServiceExtraOption>();
                try
                {
                    return JsonSerializer.Deserialize<List<ServiceExtraOption>>(ExtraOptionsJson);
                }
                catch
                {
                    return new List<ServiceExtraOption>();
                }
            }
            set
            {
                ExtraOptionsJson = JsonSerializer.Serialize(value);
            }
        }

        public virtual ICollection<OrderService> OrderServices { get; set; } = new List<OrderService>();
    }

    /// <summary>
    /// Модель для дополнительной опции/эффекта услуги
    /// </summary>
    public class ServiceExtraOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; } // effect, frame, filter
        public bool IsAvailable { get; set; } = true;
    }

    /// <summary>
    /// DTO для создания/обновления услуги с опциями
    /// </summary>
    public class ServiceCreateDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ImageUrl { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public List<ServiceExtraOptionDto>? ExtraOptions { get; set; }
    }

    /// <summary>
    /// DTO для дополнительной опции
    /// </summary>
    public class ServiceExtraOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? Icon { get; set; }
        public string? Category { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}