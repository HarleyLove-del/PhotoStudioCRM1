using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;

namespace PhotoStudioCRM.Backend.Services
{
    public class OrderServiceLayer
    {
        private readonly AppDbContext _context;

        public OrderServiceLayer(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Order?> CreateOrderAsync(Order order, List<int> serviceIds)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                order.OrderDate = DateTime.UtcNow;
                order.Status = OrderStatus.Pending;

                await _context.Orders.AddAsync(order);
                await _context.SaveChangesAsync();

                decimal totalAmount = 0;

                foreach (var serviceId in serviceIds)
                {
                    var service = await _context.Services.FindAsync(serviceId);
                    if (service != null)
                    {
                        var orderService = new OrderService
                        {
                            OrderId = order.Id,
                            ServiceId = serviceId,
                            PriceAtTime = service.Price
                        };

                        await _context.OrderServices.AddAsync(orderService);
                        totalAmount += service.Price;
                    }
                }

                order.TotalAmount = totalAmount;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // ИСПРАВЛЕНО: Возвращаем только нужные поля, без навигационных свойств
                // Создаем новый объект Order только с необходимыми полями
                var result = new Order
                {
                    Id = order.Id,
                    OrderDate = order.OrderDate,
                    TotalAmount = order.TotalAmount,
                    Status = order.Status
                };

                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return false;

            order.Status = status;
            if (status == OrderStatus.Completed)
            {
                order.CompletionDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Order?> GetOrderWithDetailsAsync(int orderId)
        {
            return await _context.Orders
                .Include(o => o.Client)
                .Include(o => o.Photographer)
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<List<Order>> GetClientOrdersAsync(int clientId)
        {
            return await _context.Orders
                .Include(o => o.OrderServices)
                    .ThenInclude(os => os.Service)
                .Where(o => o.ClientId == clientId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<bool> AddPaymentAsync(Payment payment)
        {
            payment.PaymentDate = DateTime.UtcNow;
            await _context.Payments.AddAsync(payment);
            await _context.SaveChangesAsync();

            // Update order total amount if needed
            var order = await _context.Orders.FindAsync(payment.OrderId);
            if (order != null)
            {
                var totalPaid = await _context.Payments
                    .Where(p => p.OrderId == payment.OrderId)
                    .SumAsync(p => p.Amount);
            }

            return true;
        }
    }
}