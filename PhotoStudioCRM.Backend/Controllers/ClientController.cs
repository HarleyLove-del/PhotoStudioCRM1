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
    [Authorize(Roles = "Client")]
    public class ClientController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly OrderServiceLayer _orderService;

        public ClientController(AppDbContext context, OrderServiceLayer orderService)
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

            var myOrders = await _orderService.GetClientOrdersAsync(userId);
            var recentOrders = myOrders.Take(5).Select(o => new
            {
                o.Id,
                o.OrderDate,
                o.TotalAmount,
                o.Status,
                o.PaymentMethod,
                o.PaymentStatus
            }).ToList();

            var stats = new
            {
                TotalOrders = myOrders.Count,
                CompletedOrders = myOrders.Count(o => o.Status == OrderStatus.Completed),
                PendingOrders = myOrders.Count(o => o.Status == OrderStatus.Pending),
                TotalSpent = myOrders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount)
            };

            return Ok(new { stats, recentOrders });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetCurrentUserId();
            var orders = await _orderService.GetClientOrdersAsync(userId);

            // Возвращаем заказы с полями оплаты
            var ordersWithPayment = orders.Select(o => new
            {
                o.Id,
                o.OrderDate,
                o.TotalAmount,
                o.Status,
                o.PaymentMethod,
                o.PaymentStatus,
                o.Notes,
                o.ClientId,
                o.PhotographerId
            });

            return Ok(ordersWithPayment);
        }

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            var userId = GetCurrentUserId();
            var order = await _orderService.GetOrderWithDetailsAsync(id);

            if (order == null || order.ClientId != userId)
                return NotFound();

            return Ok(order);
        }

        // ИСПРАВЛЕННЫЙ МЕТОД CreateOrder - с добавлением способа оплаты
        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();

                var order = new Order
                {
                    ClientId = userId,
                    Notes = request.Notes,
                    PaymentMethod = request.PaymentMethod,     // ДОБАВЛЕНО - способ оплаты
                    PaymentStatus = PaymentStatus.NotPaid      // ДОБАВЛЕНО - начальный статус оплаты
                };

                var createdOrder = await _orderService.CreateOrderAsync(order, request.ServiceIds);

                if (createdOrder == null)
                    return BadRequest(new { message = "Failed to create order" });

                // Возвращаем объект с информацией об оплате
                return Ok(new
                {
                    id = createdOrder.Id,
                    orderDate = createdOrder.OrderDate,
                    totalAmount = createdOrder.TotalAmount,
                    status = createdOrder.Status,
                    paymentMethod = createdOrder.PaymentMethod,
                    paymentStatus = createdOrder.PaymentStatus,
                    message = "Order created successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        // МЕТОД ОТМЕНЫ ЗАКАЗА КЛИЕНТОМ
        [HttpPut("orders/{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.ClientId == userId);

            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            if (order.Status == OrderStatus.Completed)
                return BadRequest(new { message = "Нельзя отменить выполненный заказ" });

            if (order.Status == OrderStatus.Cancelled)
                return BadRequest(new { message = "Заказ уже отменен" });

            order.Status = OrderStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Заказ успешно отменен", orderId = order.Id, status = order.Status });
        }

        [HttpGet("services")]
        public async Task<IActionResult> GetAvailableServices()
        {
            var services = await _context.Services
                .Where(s => s.IsActive)
                .ToListAsync();

            return Ok(services);
        }

        // ИСПРАВЛЕННЫЙ МЕТОД MakePayment - с обновлением статуса оплаты заказа
        [HttpPost("payments")]
        public async Task<IActionResult> MakePayment([FromBody] PaymentRequest request)
        {
            var order = await _context.Orders.FindAsync(request.OrderId);
            if (order == null || order.ClientId != GetCurrentUserId())
                return NotFound();

            var payment = new Payment
            {
                OrderId = request.OrderId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                TransactionId = Guid.NewGuid().ToString()
            };

            var success = await _orderService.AddPaymentAsync(payment);

            if (!success)
                return BadRequest(new { message = "Payment failed" });

            // Обновляем статус оплаты заказа
            var totalPaid = await _context.Payments
                .Where(p => p.OrderId == request.OrderId)
                .SumAsync(p => p.Amount);

            if (totalPaid >= order.TotalAmount)
            {
                order.PaymentStatus = PaymentStatus.Paid;
            }
            else if (totalPaid > 0)
            {
                order.PaymentStatus = PaymentStatus.PartiallyPaid;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Payment successful",
                paymentAmount = request.Amount,
                totalPaid = totalPaid,
                remainingAmount = order.TotalAmount - totalPaid,
                paymentStatus = order.PaymentStatus
            });
        }

        // МЕТОД - ПОЛУЧЕНИЕ ФОТОГРАФОВ
        [HttpGet("photographers")]
        public async Task<IActionResult> GetPhotographers()
        {
            var photographers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.Name == "Photographer" && u.IsActive)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.Phone,
                    u.IsActive
                })
                .ToListAsync();

            return Ok(photographers);
        }

        // НОВЫЙ МЕТОД - ПОЛУЧЕНИЕ ИНФОРМАЦИИ О ПЛАТЕЖАХ ПО ЗАКАЗУ
        [HttpGet("orders/{id}/payments")]
        public async Task<IActionResult> GetOrderPayments(int id)
        {
            var userId = GetCurrentUserId();
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.ClientId == userId);

            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            var payments = await _context.Payments
                .Where(p => p.OrderId == id)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.PaymentDate,
                    p.PaymentMethod,
                    p.TransactionId
                })
                .ToListAsync();

            var totalPaid = payments.Sum(p => p.Amount);
            var remainingAmount = order.TotalAmount - totalPaid;

            return Ok(new
            {
                orderId = id,
                totalAmount = order.TotalAmount,
                totalPaid = totalPaid,
                remainingAmount = remainingAmount,
                paymentStatus = order.PaymentStatus,
                payments = payments
            });
        }
    }

    public class CreateOrderRequest
    {
        public List<int> ServiceIds { get; set; } = new();
        public string? Notes { get; set; }
        public string? PaymentMethod { get; set; }  // ДОБАВЛЕНО - способ оплаты
    }

    public class PaymentRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
    }
}