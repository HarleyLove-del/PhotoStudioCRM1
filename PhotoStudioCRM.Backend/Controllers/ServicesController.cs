using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;

namespace PhotoStudioCRM.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ServicesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllServices([FromQuery] bool? activeOnly)
        {
            var query = _context.Services.AsQueryable();

            if (activeOnly == true)
                query = query.Where(s => s.IsActive);

            var services = await query
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Price,
                    s.DurationMinutes,
                    s.IsActive,
                    s.ImageUrl,
                    s.Icon,
                    s.Category,
                    ExtraOptions = s.ExtraOptions != null ? s.ExtraOptions.Select(o => new
                    {
                        o.Id,
                        o.Name,
                        o.Description,
                        o.Price,
                        o.Icon,
                        o.Category,
                        o.IsAvailable
                    }) : null
                })
                .ToListAsync();

            return Ok(services);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetServiceById(int id)
        {
            var service = await _context.Services
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.Price,
                    s.DurationMinutes,
                    s.IsActive,
                    s.ImageUrl,
                    s.Icon,
                    s.Category,
                    ExtraOptions = s.ExtraOptions != null ? s.ExtraOptions.Select(o => new
                    {
                        o.Id,
                        o.Name,
                        o.Description,
                        o.Price,
                        o.Icon,
                        o.Category,
                        o.IsAvailable
                    }) : null
                })
                .FirstOrDefaultAsync(s => s.Id == id);

            if (service == null)
                return NotFound();

            return Ok(service);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateService([FromBody] ServiceCreateDto request)
        {
            var service = new Service
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                DurationMinutes = request.DurationMinutes,
                IsActive = request.IsActive,
                ImageUrl = request.ImageUrl,
                Icon = request.Icon ?? GetDefaultIcon(request.Name),
                Category = request.Category ?? GetDefaultCategory(request.Name)
            };

            if (request.ExtraOptions != null && request.ExtraOptions.Any())
            {
                service.ExtraOptions = request.ExtraOptions.Select(o => new ServiceExtraOption
                {
                    Id = o.Id,
                    Name = o.Name,
                    Description = o.Description,
                    Price = o.Price,
                    Icon = o.Icon ?? "✨",
                    Category = o.Category ?? "effect",
                    IsAvailable = o.IsAvailable
                }).ToList();
            }

            await _context.Services.AddAsync(service);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetServiceById), new { id = service.Id }, new { message = "Услуга создана", serviceId = service.Id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateService(int id, [FromBody] ServiceCreateDto request)
        {
            var existingService = await _context.Services.FindAsync(id);

            if (existingService == null)
                return NotFound();

            existingService.Name = request.Name;
            existingService.Description = request.Description;
            existingService.Price = request.Price;
            existingService.DurationMinutes = request.DurationMinutes;
            existingService.IsActive = request.IsActive;
            existingService.ImageUrl = request.ImageUrl;
            existingService.Icon = request.Icon;
            existingService.Category = request.Category;

            if (request.ExtraOptions != null)
            {
                existingService.ExtraOptions = request.ExtraOptions.Select(o => new ServiceExtraOption
                {
                    Id = o.Id,
                    Name = o.Name,
                    Description = o.Description,
                    Price = o.Price,
                    Icon = o.Icon ?? "✨",
                    Category = o.Category ?? "effect",
                    IsAvailable = o.IsAvailable
                }).ToList();
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Услуга обновлена", service = existingService });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteService(int id)
        {
            var service = await _context.Services.FindAsync(id);

            if (service == null)
                return NotFound();

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Услуга удалена успешно" });
        }

        // Вспомогательные методы для определения иконки и категории по умолчанию
        private string GetDefaultIcon(string name)
        {
            if (string.IsNullOrEmpty(name)) return "📷";

            if (name.Contains("Портрет")) return "📸";
            if (name.Contains("Свадеб")) return "💍";
            if (name.Contains("Love")) return "💕";
            if (name.Contains("Семейн")) return "👨‍👩‍👧‍👦";
            if (name.Contains("Детск")) return "🧸";
            if (name.Contains("Беремен")) return "🤰";
            if (name.Contains("Предмет")) return "📦";
            if (name.Contains("Ретуш")) return "🎨";
            if (name.Contains("Аренд")) return "🏠";
            if (name.Contains("Видео")) return "🎥";
            return "📷";
        }

        private string GetDefaultCategory(string name)
        {
            if (string.IsNullOrEmpty(name)) return "photo";

            if (name.Contains("Видео")) return "video";
            if (name.Contains("Ретуш") || name.Contains("Обработк") || name.Contains("Редактир")) return "edit";
            if (name.Contains("Аренд") || name.Contains("Студи")) return "studio";
            return "photo";
        }
    }
}