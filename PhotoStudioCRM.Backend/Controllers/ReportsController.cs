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
    [Authorize(Roles = "Admin,Accountant")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ReportService _reportService;

        public ReportsController(AppDbContext context, ReportService reportService)
        {
            _context = context;
            _reportService = reportService;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("orders-summary")]
        public async Task<IActionResult> GetOrdersSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
            var end = endDate ?? DateTime.UtcNow;

            var orders = await _context.Orders
                .Where(o => o.OrderDate >= start && o.OrderDate <= end)
                .ToListAsync();

            var summary = new
            {
                TotalOrders = orders.Count,
                ByStatus = orders.GroupBy(o => o.Status).Select(g => new { Status = g.Key, Count = g.Count() }),
                TotalRevenue = orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
                AverageOrderValue = orders.Any() ? orders.Average(o => o.TotalAmount) : 0,
                Period = new { Start = start, End = end }
            };

            return Ok(summary);
        }

        [HttpGet("services-popularity")]
        public async Task<IActionResult> GetServicesPopularity()
        {
            var orderServices = await _context.OrderServices
                .Include(os => os.Service)
                .GroupBy(os => os.ServiceId)
                .Select(g => new
                {
                    ServiceId = g.Key,
                    ServiceName = g.First().Service != null ? g.First().Service.Name : "Unknown",
                    TimesOrdered = g.Count(),
                    TotalRevenue = g.Sum(os => os.PriceAtTime)
                })
                .OrderByDescending(x => x.TimesOrdered)
                .ToListAsync();

            return Ok(orderServices);
        }

        [HttpGet("client-statistics")]
        public async Task<IActionResult> GetClientStatistics()
        {
            var clientStats = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.Name == "Client")
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    TotalOrders = u.Orders.Count,
                    TotalSpent = u.Orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount),
                    LastOrderDate = u.Orders.Max(o => (DateTime?)o.OrderDate)
                })
                .OrderByDescending(x => x.TotalSpent)
                .ToListAsync();

            return Ok(clientStats);
        }
    }
}