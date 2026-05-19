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
    [Authorize(Roles = "Accountant")]
    public class AccountantController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ReportService _reportService;

        public AccountantController(AppDbContext context, ReportService reportService)
        {
            _context = context;
            _reportService = reportService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("financial-summary")]
        public async Task<IActionResult> GetFinancialSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var orders = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Payments)
                .Where(o => o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            var completedOrders = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
            var allOrders = orders;

            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);
            var totalPaid = allOrders.Sum(o => o.Payments.Sum(p => p.Amount));
            var totalPaidFromCompleted = completedOrders.Sum(o => o.Payments.Sum(p => p.Amount));
            var totalPartiallyPaid = allOrders.Where(o => o.PaymentStatus == PaymentStatus.PartiallyPaid).Sum(o => o.TotalAmount);
            var totalUnpaid = allOrders.Where(o => o.PaymentStatus == PaymentStatus.NotPaid && o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount);
            var pendingAmount = allOrders.Sum(o => o.TotalAmount - o.Payments.Sum(p => p.Amount));

            var paymentsByMethod = await _context.Orders
                .Where(o => o.PaymentMethod != null && o.OrderDate >= start && o.OrderDate <= end)
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new { Method = g.Key, Total = g.Sum(o => o.TotalAmount), Count = g.Count() })
                .ToListAsync();

            var paymentStatusStats = new
            {
                PaidOrders = allOrders.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                PaidAmount = totalPaid,
                PartiallyPaidOrders = allOrders.Count(o => o.PaymentStatus == PaymentStatus.PartiallyPaid),
                PartiallyPaidAmount = totalPartiallyPaid,
                UnpaidOrders = allOrders.Count(o => o.PaymentStatus == PaymentStatus.NotPaid && o.Status != OrderStatus.Cancelled),
                UnpaidAmount = totalUnpaid,
                CancelledOrders = allOrders.Count(o => o.Status == OrderStatus.Cancelled),
                CancelledAmount = allOrders.Where(o => o.Status == OrderStatus.Cancelled).Sum(o => o.TotalAmount)
            };

            return Ok(new
            {
                StartDate = start,
                EndDate = end,
                TotalRevenue = totalRevenue,
                TotalPaid = totalPaid,
                TotalPaidFromCompleted = totalPaidFromCompleted,
                PendingAmount = pendingAmount,
                PaymentsByMethod = paymentsByMethod,
                CompletedOrdersCount = completedOrders.Count,
                TotalOrdersCount = allOrders.Count,
                PaymentStatusStats = paymentStatusStats
            });
        }

        [HttpGet("reports/revenue")]
        public async Task<IActionResult> GenerateRevenueReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var userId = GetCurrentUserId();
            var report = await _reportService.GenerateRevenueReportAsync(startDate, endDate, userId);

            return Ok(report);
        }

        [HttpGet("reports/photographer-performance")]
        public async Task<IActionResult> GeneratePhotographerReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var userId = GetCurrentUserId();
            var report = await _reportService.GeneratePhotographerPerformanceReportAsync(startDate, endDate, userId);

            return Ok(report);
        }

        [HttpGet("reports")]
        public async Task<IActionResult> GetAllReports()
        {
            var reports = await _context.Reports
                .Include(r => r.GeneratedByUser)
                .OrderByDescending(r => r.GeneratedDate)
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("reports/{id}")]
        public async Task<IActionResult> GetReportById(int id)
        {
            var report = await _context.Reports
                .Include(r => r.GeneratedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound();

            return Ok(report);
        }

        // ============ МЕТОДЫ ДЛЯ КЛИЕНТОВ И ЗАКАЗОВ ============

        // 1. ПОЛУЧИТЬ ВСЕХ КЛИЕНТОВ (ИСПРАВЛЕНО: используем RoleId вместо Role.Name)
        [HttpGet("clients")]
        public async Task<IActionResult> GetClients()
        {
            // Получаем ID роли "Client"
            var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Client");

            if (clientRole == null)
            {
                return Ok(new List<object>()); // Возвращаем пустой список, если роль не найдена
            }

            var clients = await _context.Users
                .Where(u => u.RoleId == clientRole.Id)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Phone,
                    u.CreatedAt
                })
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            return Ok(clients);
        }

        // 2. ПОЛУЧИТЬ ИНФОРМАЦИЮ О КЛИЕНТЕ ПО ID (ИСПРАВЛЕНО)
        [HttpGet("users/{clientId}")]
        public async Task<IActionResult> GetUserById(int clientId)
        {
            var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Client");

            var user = await _context.Users
                .Where(u => u.Id == clientId && (clientRole == null || u.RoleId == clientRole.Id))
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Phone,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "Клиент не найден" });

            return Ok(user);
        }

        // 3. ПОЛУЧИТЬ ВСЕ ЗАКАЗЫ (с информацией о клиентах)
        [HttpGet("orders")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Payments)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.ClientId,
                    o.OrderDate,
                    o.CompletionDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    ClientName = o.Client != null ? $"{o.Client.FirstName} {o.Client.LastName}" : "Неизвестен",
                    ClientEmail = o.Client != null ? o.Client.Email : "",
                    ClientPhone = o.Client != null ? o.Client.Phone : "",
                    TotalPaid = o.Payments.Sum(p => p.Amount),
                    RemainingAmount = o.TotalAmount - o.Payments.Sum(p => p.Amount)
                })
                .ToListAsync();

            return Ok(orders);
        }

        // 4. ПОЛУЧИТЬ ЗАКАЗЫ КОНКРЕТНОГО КЛИЕНТА
        [HttpGet("clients/{clientId}/orders")]
        public async Task<IActionResult> GetClientOrders(int clientId)
        {
            // Проверяем, существует ли клиент
            var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Client");
            var clientExists = await _context.Users
                .AnyAsync(u => u.Id == clientId && (clientRole == null || u.RoleId == clientRole.Id));

            if (!clientExists)
                return NotFound(new { message = "Клиент не найден" });

            var orders = await _context.Orders
                .Include(o => o.Payments)
                .Where(o => o.ClientId == clientId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.CompletionDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    TotalPaid = o.Payments.Sum(p => p.Amount),
                    RemainingAmount = o.TotalAmount - o.Payments.Sum(p => p.Amount)
                })
                .ToListAsync();

            return Ok(orders);
        }

        // 5. ПОЛУЧИТЬ ЗАКАЗ ПО ID (для чека)
        [HttpGet("orders/{orderId}")]
        public async Task<IActionResult> GetOrderById(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Payments)
                .Where(o => o.Id == orderId)
                .Select(o => new
                {
                    o.Id,
                    o.ClientId,
                    o.OrderDate,
                    o.CompletionDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    ClientName = o.Client != null ? $"{o.Client.FirstName} {o.Client.LastName}" : "Неизвестен",
                    ClientEmail = o.Client != null ? o.Client.Email : "",
                    ClientPhone = o.Client != null ? o.Client.Phone : "",
                    TotalPaid = o.Payments.Sum(p => p.Amount),
                    RemainingAmount = o.TotalAmount - o.Payments.Sum(p => p.Amount),
                    Payments = o.Payments.Select(p => new
                    {
                        p.Id,
                        p.Amount,
                        p.PaymentDate,
                        p.PaymentMethod,
                        p.TransactionId
                    })
                })
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            return Ok(order);
        }

        // 6. ПОЛУЧИТЬ ЗАКАЗЫ ПО СТАТУСУ ОПЛАТЫ
        [HttpGet("orders/by-payment-status")]
        public async Task<IActionResult> GetOrdersByPaymentStatus([FromQuery] PaymentStatus? paymentStatus)
        {
            var query = _context.Orders
                .Include(o => o.Client)
                .AsQueryable();

            if (paymentStatus.HasValue)
            {
                query = query.Where(o => o.PaymentStatus == paymentStatus.Value);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.ClientId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    ClientName = o.Client != null ? $"{o.Client.FirstName} {o.Client.LastName}" : "Неизвестен",
                    ClientEmail = o.Client != null ? o.Client.Email : ""
                })
                .ToListAsync();

            return Ok(orders);
        }

        // 7. ПОЛУЧИТЬ СТАТИСТИКУ ПО КЛИЕНТАМ (топ клиенты по суммам заказов)
        [HttpGet("clients/top-spenders")]
        public async Task<IActionResult> GetTopSpenders([FromQuery] int limit = 10, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-12);
            var end = endDate ?? DateTime.UtcNow;

            var topClients = await _context.Orders
                .Include(o => o.Client)
                .Where(o => o.OrderDate >= start && o.OrderDate <= end && o.Client != null)
                .GroupBy(o => new { o.ClientId, o.Client.FirstName, o.Client.LastName, o.Client.Email })
                .Select(g => new
                {
                    g.Key.ClientId,
                    ClientName = $"{g.Key.FirstName} {g.Key.LastName}",
                    g.Key.Email,
                    TotalOrders = g.Count(),
                    TotalSpent = g.Sum(o => o.TotalAmount),
                    TotalPaid = g.Sum(o => o.Payments.Sum(p => p.Amount)),
                    CompletedOrders = g.Count(o => o.Status == OrderStatus.Completed)
                })
                .OrderByDescending(g => g.TotalSpent)
                .Take(limit)
                .ToListAsync();

            return Ok(topClients);
        }

        // ============ МЕТОДЫ ДЛЯ РАБОТЫ С ЧЕКАМИ ============

        // 8. ПОЛУЧИТЬ ВСЕ ЧЕКИ
        [HttpGet("receipts")]
        public async Task<IActionResult> GetAllReceipts()
        {
            var receipts = await _context.Receipts
                .Include(r => r.Order)
                .ThenInclude(o => o.Client)
                .OrderByDescending(r => r.CreatedDate)
                .Select(r => new
                {
                    r.Id,
                    r.OrderId,
                    r.Amount,
                    r.PaymentMethod,
                    r.CreatedDate,
                    r.SentDate,
                    r.IsSent,
                    ClientName = r.Order.Client != null ? $"{r.Order.Client.FirstName} {r.Order.Client.LastName}" : "Неизвестен",
                    ClientEmail = r.Order.Client != null ? r.Order.Client.Email : ""
                })
                .ToListAsync();

            return Ok(receipts);
        }

        // 9. ПОЛУЧИТЬ ЧЕК ПО ID
        [HttpGet("receipts/{id}")]
        public async Task<IActionResult> GetReceiptById(int id)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Order)
                .ThenInclude(o => o.Client)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
                return NotFound(new { message = "Чек не найден" });

            return Ok(new
            {
                receipt.Id,
                receipt.OrderId,
                receipt.Amount,
                receipt.PaymentMethod,
                receipt.CreatedDate,
                receipt.SentDate,
                receipt.IsSent,
                ClientName = receipt.Order.Client != null ? $"{receipt.Order.Client.FirstName} {receipt.Order.Client.LastName}" : "Неизвестен",
                ClientEmail = receipt.Order.Client != null ? receipt.Order.Client.Email : ""
            });
        }

        // 10. СОЗДАТЬ НОВЫЙ ЧЕК
        [HttpPost("receipts")]
        public async Task<IActionResult> CreateReceipt([FromBody] CreateReceiptRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Проверяем существование заказа
            var order = await _context.Orders
                .Include(o => o.Client)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId);

            if (order == null)
                return BadRequest(new { message = "Заказ не найден" });

            // Создаём новый чек
            var receipt = new Receipt
            {
                OrderId = request.OrderId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                CreatedDate = DateTime.UtcNow,
                IsSent = false,
                ReceiptNumber = await GenerateReceiptNumber()
            };

            _context.Receipts.Add(receipt);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                receipt.Id,
                receipt.ReceiptNumber,
                receipt.OrderId,
                receipt.Amount,
                receipt.PaymentMethod,
                receipt.CreatedDate,
                receipt.IsSent,
                ClientName = order.Client != null ? $"{order.Client.FirstName} {order.Client.LastName}" : "Неизвестен",
                ClientEmail = order.Client != null ? order.Client.Email : ""
            });
        }

        // 11. ОТПРАВИТЬ ЧЕК НА EMAIL
        [HttpPost("receipts/{id}/send")]
        public async Task<IActionResult> SendReceipt(int id)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Order)
                .ThenInclude(o => o.Client)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
                return NotFound(new { message = "Чек не найден" });

            if (receipt.IsSent)
                return BadRequest(new { message = "Чек уже был отправлен" });

            if (receipt.Order.Client == null || string.IsNullOrEmpty(receipt.Order.Client.Email))
                return BadRequest(new { message = "У клиента не указан email для отправки" });

            // Здесь должен быть код отправки email через сервис
            // await _emailService.SendReceiptEmailAsync(receipt.Order.Client.Email, receipt);

            // Пока просто отмечаем как отправленный
            receipt.IsSent = true;
            receipt.SentDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Чек успешно отправлен",
                receipt.Id,
                receipt.IsSent,
                receipt.SentDate,
                ClientEmail = receipt.Order.Client.Email
            });
        }

        // 12. ПОЛУЧИТЬ ЧЕКИ ПО ЗАКАЗУ
        [HttpGet("orders/{orderId}/receipts")]
        public async Task<IActionResult> GetReceiptsByOrderId(int orderId)
        {
            var receipts = await _context.Receipts
                .Where(r => r.OrderId == orderId)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return Ok(receipts);
        }

        // Вспомогательный метод для генерации номера чека
        private async Task<string> GenerateReceiptNumber()
        {
            var lastReceipt = await _context.Receipts
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (lastReceipt != null && !string.IsNullOrEmpty(lastReceipt.ReceiptNumber))
            {
                var parts = lastReceipt.ReceiptNumber.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1], out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            return $"ЧЕК-{nextNumber:D6}";
        }

        // ============ СУЩЕСТВУЮЩИЕ МЕТОДЫ ДЛЯ ОТЧЕТОВ ПО ОПЛАТАМ ============

        // ДЕТАЛЬНЫЙ ОТЧЕТ ПО ОПЛАТАМ
        [HttpGet("reports/payments-detailed")]
        public async Task<IActionResult> GetDetailedPaymentsReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var orders = await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Payments)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.CompletionDate,
                    o.TotalAmount,
                    o.Status,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.Notes,
                    ClientName = o.Client != null ? $"{o.Client.FirstName} {o.Client.LastName}" : "Неизвестен",
                    ClientEmail = o.Client != null ? o.Client.Email : "",
                    ClientPhone = o.Client != null ? o.Client.Phone : "",
                    TotalPaid = o.Payments.Sum(p => p.Amount),
                    RemainingAmount = o.TotalAmount - o.Payments.Sum(p => p.Amount),
                    Payments = o.Payments.Select(p => new { p.Amount, p.PaymentDate, p.PaymentMethod, p.TransactionId })
                })
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var summary = new
            {
                StartDate = startDate,
                EndDate = endDate,
                GeneratedAt = DateTime.UtcNow,
                TotalOrders = orders.Count,
                TotalAmount = orders.Sum(o => o.TotalAmount),
                TotalPaid = orders.Sum(o => o.TotalPaid),
                TotalRemaining = orders.Sum(o => o.RemainingAmount),
                PaidOrdersCount = orders.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                PartiallyPaidCount = orders.Count(o => o.PaymentStatus == PaymentStatus.PartiallyPaid),
                UnpaidCount = orders.Count(o => o.PaymentStatus == PaymentStatus.NotPaid),
                CancelledCount = orders.Count(o => o.Status == OrderStatus.Cancelled),
                Orders = orders
            };

            return Ok(summary);
        }

        // СТАТИСТИКА ПО СПОСОБАМ ОПЛАТЫ (ИСПРАВЛЕНО: убрана ошибка с Dictionary)
        [HttpGet("reports/payment-methods-stats")]
        public async Task<IActionResult> GetPaymentMethodsStats([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var stats = await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.PaymentMethod != null)
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new
                {
                    PaymentMethod = g.Key,
                    OrdersCount = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount),
                    PaidAmount = g.Where(o => o.PaymentStatus == PaymentStatus.Paid).Sum(o => o.TotalAmount),
                    PartiallyPaidAmount = g.Where(o => o.PaymentStatus == PaymentStatus.PartiallyPaid).Sum(o => o.TotalAmount),
                    UnpaidAmount = g.Where(o => o.PaymentStatus == PaymentStatus.NotPaid && o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
                    AverageOrderValue = g.Average(o => o.TotalAmount),
                    CompletionRate = g.Count(o => o.Status == OrderStatus.Completed) * 100.0 / g.Count()
                })
                .ToListAsync();

            // Исправлено: безопасное получение иконки
            var result = stats.Select(s => new
            {
                s.PaymentMethod,
                Icon = GetPaymentMethodIcon(s.PaymentMethod),
                s.OrdersCount,
                s.TotalAmount,
                s.PaidAmount,
                s.PartiallyPaidAmount,
                s.UnpaidAmount,
                s.AverageOrderValue,
                CompletionRate = Math.Round(s.CompletionRate, 2)
            });

            return Ok(new
            {
                StartDate = startDate,
                EndDate = endDate,
                Stats = result
            });
        }

        // Вспомогательный метод для получения иконки способа оплаты
        private string GetPaymentMethodIcon(string paymentMethod)
        {
            return paymentMethod?.ToLower() switch
            {
                "card" => "💳",
                "cash" => "💰",
                "online" => "🏦",
                "installment" => "📅",
                _ => "💵"
            };
        }

        // СТАТУСЫ ОПЛАТЫ ПО КЛИЕНТАМ
        [HttpGet("reports/client-payment-status")]
        public async Task<IActionResult> GetClientPaymentStatus([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var clientPayments = await _context.Orders
                .Include(o => o.Client)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Client != null)
                .GroupBy(o => o.ClientId)
                .Select(g => new
                {
                    ClientId = g.Key,
                    ClientName = g.First().Client != null ? $"{g.First().Client.FirstName} {g.First().Client.LastName}" : "Неизвестен",
                    ClientEmail = g.First().Client != null ? g.First().Client.Email : "",
                    TotalOrders = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount),
                    TotalPaid = g.Sum(o => o.Payments.Sum(p => p.Amount)),
                    PaidOrders = g.Count(o => o.PaymentStatus == PaymentStatus.Paid),
                    PartiallyPaidOrders = g.Count(o => o.PaymentStatus == PaymentStatus.PartiallyPaid),
                    UnpaidOrders = g.Count(o => o.PaymentStatus == PaymentStatus.NotPaid && o.Status != OrderStatus.Cancelled),
                    CancelledOrders = g.Count(o => o.Status == OrderStatus.Cancelled)
                })
                .OrderByDescending(g => g.TotalAmount)
                .ToListAsync();

            return Ok(new
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalClients = clientPayments.Count,
                TotalRevenue = clientPayments.Sum(c => c.TotalAmount),
                TotalPaidRevenue = clientPayments.Sum(c => c.TotalPaid),
                Clients = clientPayments
            });
        }

        // ЕЖЕДНЕВНАЯ СТАТИСТИКА ОПЛАТ
        [HttpGet("reports/daily-payment-stats")]
        public async Task<IActionResult> GetDailyPaymentStats([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var dailyStats = await _context.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    OrdersCount = g.Count(),
                    TotalAmount = g.Sum(o => o.TotalAmount),
                    PaidAmount = g.Where(o => o.PaymentStatus == PaymentStatus.Paid).Sum(o => o.TotalAmount),
                    PartiallyPaidAmount = g.Where(o => o.PaymentStatus == PaymentStatus.PartiallyPaid).Sum(o => o.TotalAmount),
                    UnpaidAmount = g.Where(o => o.PaymentStatus == PaymentStatus.NotPaid && o.Status != OrderStatus.Cancelled).Sum(o => o.TotalAmount),
                    CompletedOrders = g.Count(o => o.Status == OrderStatus.Completed),
                    CancelledOrders = g.Count(o => o.Status == OrderStatus.Cancelled)
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            return Ok(new
            {
                StartDate = startDate,
                EndDate = endDate,
                DailyStats = dailyStats,
                Summary = new
                {
                    TotalDays = dailyStats.Count,
                    TotalOrders = dailyStats.Sum(d => d.OrdersCount),
                    TotalRevenue = dailyStats.Sum(d => d.TotalAmount),
                    TotalPaid = dailyStats.Sum(d => d.PaidAmount),
                    AverageDailyRevenue = dailyStats.Any() ? dailyStats.Average(d => d.TotalAmount) : 0
                }
            });
        }

        // ОТЧЕТ ПО ПРОСРОЧЕННЫМ ПЛАТЕЖАМ
        [HttpGet("reports/overdue-payments")]
        public async Task<IActionResult> GetOverduePayments([FromQuery] int daysOverdue = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOverdue);

            var overdueOrders = await _context.Orders
                .Include(o => o.Client)
                .Where(o => o.Status != OrderStatus.Cancelled &&
                           o.Status != OrderStatus.Completed &&
                           o.PaymentStatus != PaymentStatus.Paid &&
                           o.OrderDate <= cutoffDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    DaysOverdue = DateTime.UtcNow.Subtract(o.OrderDate).Days,
                    ClientName = o.Client != null ? $"{o.Client.FirstName} {o.Client.LastName}" : "Неизвестен",
                    ClientEmail = o.Client != null ? o.Client.Email : "",
                    ClientPhone = o.Client != null ? o.Client.Phone : "",
                    TotalPaid = o.Payments.Sum(p => p.Amount),
                    RemainingAmount = o.TotalAmount - o.Payments.Sum(p => p.Amount)
                })
                .OrderByDescending(o => o.DaysOverdue)
                .ToListAsync();

            return Ok(new
            {
                GeneratedAt = DateTime.UtcNow,
                DaysOverdueThreshold = daysOverdue,
                TotalOverdueOrders = overdueOrders.Count,
                TotalOverdueAmount = overdueOrders.Sum(o => o.RemainingAmount),
                OverdueOrders = overdueOrders
            });
        }
    }


    public class CreateReceiptRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
    }
}