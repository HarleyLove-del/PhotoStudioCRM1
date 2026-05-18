using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;
using PhotoStudioCRM.Backend.Services;

namespace PhotoStudioCRM.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly AppDbContext _context;

        public AuthController(AuthService authService, AppDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Валидация входных данных
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { message = "Email и пароль обязательны для заполнения" });
            }

            var token = await _authService.AuthenticateAsync(request.Email, request.Password);

            if (token == null)
                return Unauthorized(new { message = "Неверный email или пароль" });

            return Ok(new { token });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Валидация входных данных
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { message = "Email обязателен для заполнения" });

            if (string.IsNullOrEmpty(request.Password))
                return BadRequest(new { message = "Пароль обязателен для заполнения" });

            if (request.Password.Length < 6)
                return BadRequest(new { message = "Пароль должен содержать минимум 6 символов" });

            if (string.IsNullOrEmpty(request.FirstName))
                return BadRequest(new { message = "Имя обязательно для заполнения" });

            if (string.IsNullOrEmpty(request.LastName))
                return BadRequest(new { message = "Фамилия обязательна для заполнения" });

            // Проверяем, не существует ли пользователь с таким email
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { message = "Пользователь с таким email уже существует" });
            }

            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // Если роль не указана, всегда регистрируем как Client
            var role = string.IsNullOrEmpty(request.Role) ? "Client" : request.Role;

            var createdUser = await _authService.RegisterUserAsync(user, request.Password, role);

            if (createdUser == null)
                return BadRequest(new { message = "Ошибка регистрации. Возможно, указанная роль не существует." });

            return Ok(new
            {
                message = "Регистрация успешна! Теперь вы можете войти в систему.",
                userId = createdUser.Id,
                email = createdUser.Email,
                firstName = createdUser.FirstName,
                lastName = createdUser.LastName
            });
        }

        // Дополнительный метод для проверки существования пользователя
        [HttpPost("check-email")]
        public async Task<IActionResult> CheckEmail([FromBody] CheckEmailRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { message = "Email обязателен" });

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            return Ok(new
            {
                exists = existingUser != null,
                message = existingUser != null ? "Email уже используется" : "Email свободен"
            });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Role { get; set; }
    }

    public class CheckEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}