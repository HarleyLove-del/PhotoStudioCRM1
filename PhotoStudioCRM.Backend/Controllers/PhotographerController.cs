using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;
using PhotoStudioCRM.Backend.Services;
using System.Security.Claims;

namespace PhotoStudioCRM.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Photographer")]
    public class PhotographerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly OrderServiceLayer _orderService;

        public PhotographerController(AppDbContext context, OrderServiceLayer orderService)
        {
            _context = context;
            _orderService = orderService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var userId = GetCurrentUserId();

            // Заказы, назначенные этому фотографу (с информацией об оплате)
            var myOrders = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .Where(o => o.PhotographerId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName, o.Client.Phone } : null,
                    Services = o.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime })
                })
                .ToListAsync();

            // Заказы в статусе "Ожидает" (нужно принять) с информацией об оплате
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
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName, o.Client.Phone } : null,
                    Services = o.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime })
                })
                .ToListAsync();

            var mySchedule = await _context.Schedules
                .Where(s => s.PhotographerId == userId && s.StartTime >= DateTime.UtcNow)
                .OrderBy(s => s.StartTime)
                .Take(5)
                .ToListAsync();

            var stats = new
            {
                TotalOrders = myOrders.Count,
                CompletedOrders = myOrders.Count(o => o.Status == OrderStatus.Completed),
                InProgressOrders = myOrders.Count(o => o.Status == OrderStatus.InProgress),
                PendingOrders = pendingOrders.Count,
                TotalPaidOrders = myOrders.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                TotalRevenue = myOrders.Where(o => o.PaymentStatus == PaymentStatus.Paid).Sum(o => o.TotalAmount)
            };

            return Ok(new { stats, mySchedule, myOrders, pendingOrders });
        }

        [HttpGet("assigned-orders")]
        public async Task<IActionResult> GetAssignedOrders()
        {
            var userId = GetCurrentUserId();
            var orders = await _context.Orders
                .Where(o => o.PhotographerId == userId)
                .Include(o => o.Client)
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName, o.Client.Email, o.Client.Phone } : null,
                    Services = o.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime, os.Service.DurationMinutes })
                })
                .ToListAsync();

            return Ok(orders);
        }

        [HttpGet("pending-orders")]
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
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    Client = o.Client != null ? new { o.Client.FirstName, o.Client.LastName, o.Client.Email, o.Client.Phone } : null,
                    Services = o.OrderServices.Select(os => new { os.Service.Name, os.PriceAtTime })
                })
                .ToListAsync();

            return Ok(pendingOrders);
        }

        // ПРИНЯТИЕ ЗАКАЗА
        [HttpPost("orders/{id}/accept")]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            if (order.Status != OrderStatus.Pending)
                return BadRequest(new { message = "Этот заказ уже обработан" });

            if (order.PhotographerId != null)
                return BadRequest(new { message = "Заказ уже принят другим фотографом" });

            order.PhotographerId = userId;
            order.Status = OrderStatus.Confirmed;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Заказ успешно принят!", orderId = order.Id, status = order.Status });
        }

        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateStatusRequest request)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders.FindAsync(id);

            if (order == null || order.PhotographerId != userId)
                return NotFound();

            var success = await _orderService.UpdateOrderStatusAsync(id, request.Status);

            if (!success)
                return BadRequest(new { message = "Failed to update status" });

            return Ok(new { message = "Status updated successfully", status = order.Status });
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> AddSchedule([FromBody] ScheduleRequest request)
        {
            var userId = GetCurrentUserId();

            var schedule = new Schedule
            {
                PhotographerId = userId,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsAvailable = true
            };

            await _context.Schedules.AddAsync(schedule);
            await _context.SaveChangesAsync();

            return Ok(schedule);
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetMySchedule([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var userId = GetCurrentUserId();
            var query = _context.Schedules.Where(s => s.PhotographerId == userId);

            if (startDate.HasValue)
                query = query.Where(s => s.StartTime >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(s => s.EndTime <= endDate.Value);

            var schedule = await query.OrderBy(s => s.StartTime).ToListAsync();
            return Ok(schedule);
        }
    }

    public class UpdateStatusRequest
    {
        public OrderStatus Status { get; set; }
    }

    public class ScheduleRequest
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}