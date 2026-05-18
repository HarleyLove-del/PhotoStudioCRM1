using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;
using PhotoStudioCRM.Backend.Services;

namespace PhotoStudioCRM.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AuthService _authService;

        public AdminController(AppDbContext context, AuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // ============ УПРАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯМИ ============

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.Phone,
                    u.CreatedAt,
                    u.IsActive,
                    Role = u.Role != null ? u.Role.Name : null
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var user = new User
            {
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone
            };

            var createdUser = await _authService.RegisterUserAsync(user, request.Password, request.Role);

            if (createdUser == null)
                return BadRequest(new { message = "Failed to create user" });

            return Ok(new { message = "User created successfully", userId = createdUser.Id });
        }

        [HttpPut("users/{id}/activate")]
        public async Task<IActionResult> ToggleUserActivation(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User {(user.IsActive ? "activated" : "deactivated")}" });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User deleted successfully" });
        }

        // ============ СТАТИСТИКА ============

        [HttpGet("dashboard/stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalOrders = await _context.Orders.CountAsync();
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Completed && o.PaymentStatus == PaymentStatus.Paid)
                .SumAsync(o => o.TotalAmount);

            var recentOrders = await _context.Orders
                .Include(o => o.Client)
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName } : null
                })
                .ToListAsync();

            var ordersByStatus = await _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var paymentStats = new
            {
                TotalPaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.Paid),
                TotalPartiallyPaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.PartiallyPaid),
                TotalUnpaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.NotPaid),
                TotalPaidAmount = await _context.Orders.Where(o => o.PaymentStatus == PaymentStatus.Paid).SumAsync(o => o.TotalAmount),
                TotalPendingAmount = await _context.Orders.Where(o => o.PaymentStatus != PaymentStatus.Paid && o.Status != OrderStatus.Cancelled).SumAsync(o => o.TotalAmount)
            };

            return Ok(new
            {
                TotalUsers = totalUsers,
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                OrdersByStatus = ordersByStatus,
                RecentOrders = recentOrders,
                PaymentStats = paymentStats
            });
        }

        // ============ УПРАВЛЕНИЕ ЗАКАЗАМИ С ОПЛАТОЙ ============

        // Получение всех заказов с информацией об оплате
        [HttpGet("orders")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Photographer)
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .Include(o => o.Payments)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.CompletionDate,
                    o.TotalAmount,
                    o.Status,
                    o.Notes,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.ClientId,
                    o.PhotographerId,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName, o.Client.Email, o.Client.Phone } : null,
                    Photographer = o.Photographer != null ? new { o.Photographer.FirstName, o.Photographer.LastName, o.Photographer.Email } : null,
                    Services = o.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime, os.Service.DurationMinutes }),
                    TotalPaid = o.Payments.Sum(p => p.Amount),
                    RemainingAmount = o.TotalAmount - o.Payments.Sum(p => p.Amount),
                    Payments = o.Payments.Select(p => new { p.Id, p.Amount, p.PaymentDate, p.PaymentMethod })
                })
                .ToListAsync();

            return Ok(orders);
        }

        // Получение заказов в статусе "Ожидает" (без фотографа)
        [HttpGet("orders/pending")]
        public async Task<IActionResult> GetPendingOrders()
        {
            var pendingOrders = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .Where(o => o.Status == OrderStatus.Pending && o.PhotographerId == null)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.Notes,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.ClientId,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName, o.Client.Email, o.Client.Phone } : null,
                    Services = o.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime })
                })
                .ToListAsync();

            return Ok(pendingOrders);
        }

        // Назначение заказа фотографу
        [HttpPost("orders/{orderId}/assign/{photographerId}")]
        public async Task<IActionResult> AssignOrderToPhotographer(int orderId, int photographerId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            if (order.Status != OrderStatus.Pending)
                return BadRequest(new { message = "Можно назначать только заказы в статусе 'Ожидает'" });

            var photographer = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == photographerId && u.Role.Name == "Photographer");

            if (photographer == null)
                return NotFound(new { message = "Фотограф не найден" });

            if (!photographer.IsActive)
                return BadRequest(new { message = "Фотограф неактивен" });

            order.PhotographerId = photographerId;
            order.Status = OrderStatus.Confirmed;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Заказ #{orderId} назначен фотографу {photographer.FirstName} {photographer.LastName}",
                orderId = orderId,
                photographerId = photographerId,
                photographerName = $"{photographer.FirstName} {photographer.LastName}"
            });
        }

        // Отмена заказа админом
        [HttpPut("orders/{id}/cancel")]
        public async Task<IActionResult> CancelOrderByAdmin(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Client)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            if (order.Status == OrderStatus.Completed)
                return BadRequest(new { message = "Нельзя отменить выполненный заказ" });

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest(new { message = "Заказ уже отменен" });

            var oldStatus = order.Status;
            order.Status = OrderStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Заказ #{id} отменен администратором", orderId = order.Id, oldStatus = oldStatus, newStatus = order.Status });
        }

        // Переназначение заказа (если фотограф занят)
        [HttpPut("orders/{orderId}/reassign/{newPhotographerId}")]
        public async Task<IActionResult> ReassignOrder(int orderId, int newPhotographerId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            var newPhotographer = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == newPhotographerId && u.Role.Name == "Photographer");

            if (newPhotographer == null)
                return NotFound(new { message = "Фотограф не найден" });

            if (!newPhotographer.IsActive)
                return BadRequest(new { message = "Фотограф неактивен" });

            var oldPhotographerId = order.PhotographerId;
            var oldPhotographerName = oldPhotographerId != null
                ? (await _context.Users.FindAsync(oldPhotographerId))?.FirstName
                : "не был назначен";

            order.PhotographerId = newPhotographerId;
            order.Status = OrderStatus.Confirmed;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Заказ #{orderId} переназначен",
                orderId = orderId,
                oldPhotographer = oldPhotographerName,
                newPhotographer = $"{newPhotographer.FirstName} {newPhotographer.LastName}"
            });
        }

        // Получение деталей конкретного заказа
        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Photographer)
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            var result = new
            {
                order.Id,
                order.OrderDate,
                order.CompletionDate,
                order.TotalAmount,
                order.Status,
                order.Notes,
                order.PaymentMethod,
                order.PaymentStatus,
                order.ClientId,
                order.PhotographerId,
                Client = order.Client != null ? new { order.Client.FirstName, order.Client.LastName, order.Client.Email, order.Client.Phone } : null,
                Photographer = order.Photographer != null ? new { order.Photographer.FirstName, order.Photographer.LastName, order.Photographer.Email } : null,
                Services = order.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime, os.Service.DurationMinutes }),
                Payments = order.Payments.Select(p => new { p.Id, p.Amount, p.PaymentDate, p.PaymentMethod, p.TransactionId }),
                TotalPaid = order.Payments.Sum(p => p.Amount),
                RemainingAmount = order.TotalAmount - order.Payments.Sum(p => p.Amount)
            };

            return Ok(result);
        }

        // Отметить заказ как оплаченный
        [HttpPut("orders/{id}/mark-paid")]
        public async Task<IActionResult> MarkOrderAsPaid(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            if (order.PaymentStatus == PaymentStatus.Paid)
                return BadRequest(new { message = "Заказ уже оплачен" });

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest(new { message = "Нельзя отметить оплату отмененного заказа" });

            order.PaymentStatus = PaymentStatus.Paid;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Заказ #{id} отмечен как оплаченный",
                orderId = order.Id,
                paymentStatus = order.PaymentStatus
            });
        }

        // Получение статистики по платежам
        [HttpGet("payments/stats")]
        public async Task<IActionResult> GetPaymentStats()
        {
            var stats = new
            {
                TotalPaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.Paid),
                TotalPartiallyPaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.PartiallyPaid),
                TotalUnpaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.NotPaid),
                TotalPaidAmount = await _context.Orders.Where(o => o.PaymentStatus == PaymentStatus.Paid).SumAsync(o => o.TotalAmount),
                TotalPartiallyPaidAmount = await _context.Orders.Where(o => o.PaymentStatus == PaymentStatus.PartiallyPaid).SumAsync(o => o.TotalAmount),
                TotalPendingAmount = await _context.Orders.Where(o => o.PaymentStatus != PaymentStatus.Paid && o.Status != OrderStatus.Cancelled).SumAsync(o => o.TotalAmount),
                PaymentsByMethod = await _context.Orders
                    .Where(o => o.PaymentMethod != null)
                    .GroupBy(o => o.PaymentMethod)
                    .Select(g => new { Method = g.Key, Count = g.Count(), TotalAmount = g.Sum(o => o.TotalAmount) })
                    .ToListAsync()
            };

            return Ok(stats);
        }

        // Получение всех фотографов
        [HttpGet("photographers")]
        public async Task<IActionResult> GetPhotographers()
        {
            var photographers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.Name == "Photographer")
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.Phone,
                    u.IsActive,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(photographers);
        }

        // Получение статистики по заказам
        [HttpGet("orders/stats")]
        public async Task<IActionResult> GetOrdersStats()
        {
            var totalOrders = await _context.Orders.CountAsync();
            var pendingOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
            var inProgressOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.InProgress);
            var completedOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Completed);
            var cancelledOrders = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled);

            var totalRevenue = await _context.Orders
                .Where(o => o.Status == OrderStatus.Completed)
                .SumAsync(o => o.TotalAmount);

            var paidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.Paid);
            var partiallyPaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.PartiallyPaid);
            var unpaidOrders = await _context.Orders.CountAsync(o => o.PaymentStatus == PaymentStatus.NotPaid);

            return Ok(new
            {
                TotalOrders = totalOrders,
                PendingOrders = pendingOrders,
                InProgressOrders = inProgressOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                TotalRevenue = totalRevenue,
                PaidOrders = paidOrders,
                PartiallyPaidOrders = partiallyPaidOrders,
                UnpaidOrders = unpaidOrders
            });
        }
    }

    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string Role { get; set; } = "Client";
    }
}