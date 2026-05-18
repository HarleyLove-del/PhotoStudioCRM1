using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;
using System.Text.Json;

namespace PhotoStudioCRM.Backend.Services
{
    public class ReportService
    {
        private readonly AppDbContext _context;

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Report> GenerateRevenueReportAsync(DateTime startDate, DateTime endDate, int userId)
        {
            var completedOrders = await _context.Orders
                .Include(o => o.Payments)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && o.Status == OrderStatus.Completed)
                .ToListAsync();

            var totalRevenue = completedOrders.Sum(o => o.TotalAmount);
            var totalPayments = completedOrders.Sum(o => o.Payments.Sum(p => p.Amount));

            var reportData = new
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalRevenue = totalRevenue,
                TotalPayments = totalPayments,
                OrdersCount = completedOrders.Count,
                Orders = completedOrders.Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    Payments = o.Payments.Select(p => new { p.Amount, p.PaymentDate })
                })
            };

            var report = new Report
            {
                ReportType = "Revenue Report",
                StartDate = startDate,
                EndDate = endDate,
                Data = JsonSerializer.Serialize(reportData),
                GeneratedByUserId = userId,
                GeneratedDate = DateTime.UtcNow
            };

            await _context.Reports.AddAsync(report);
            await _context.SaveChangesAsync();

            return report;
        }

        public async Task<Report> GeneratePhotographerPerformanceReportAsync(DateTime startDate, DateTime endDate, int userId)
        {
            var photographers = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.Name == "Photographer")
                .ToListAsync();

            var performanceData = new List<object>();

            foreach (var photographer in photographers)
            {
                var orders = await _context.Orders
                    .Where(o => o.PhotographerId == photographer.Id &&
                                o.OrderDate >= startDate &&
                                o.OrderDate <= endDate)
                    .ToListAsync();

                performanceData.Add(new
                {
                    PhotographerId = photographer.Id,
                    PhotographerName = $"{photographer.FirstName} {photographer.LastName}",
                    TotalOrders = orders.Count,
                    CompletedOrders = orders.Count(o => o.Status == OrderStatus.Completed),
                    TotalRevenue = orders.Sum(o => o.TotalAmount)
                });
            }

            var report = new Report
            {
                ReportType = "Photographer Performance Report",
                StartDate = startDate,
                EndDate = endDate,
                Data = JsonSerializer.Serialize(performanceData),
                GeneratedByUserId = userId,
                GeneratedDate = DateTime.UtcNow
            };

            await _context.Reports.AddAsync(report);
            await _context.SaveChangesAsync();

            return report;
        }
    }
}