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

        // ============ НОВЫЕ МЕТОДЫ ДЛЯ ОТЧЕТОВ ПО ОПЛАТАМ ============

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

        // СТАТИСТИКА ПО СПОСОБАМ ОПЛАТЫ
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

            var methodIcons = new Dictionary<string, string>
            {
                { "card", "💳" },
                { "cash", "💰" },
                { "online", "🏦" },
                { "installment", "📅" }
            };

            var result = stats.Select(s => new
            {
                s.PaymentMethod,
                Icon = methodIcons.ContainsKey(s.PaymentMethod) ? methodIcons[s.PaymentMethod] : "💵",
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
}