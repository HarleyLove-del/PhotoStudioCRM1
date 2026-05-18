using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Data;
using PhotoStudioCRM.Backend.Models;
using System.Security.Claims;

namespace PhotoStudioCRM.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ScheduleController(AppDbContext context)
        {
            _context = context;
        }

        private int GetCurrentUserId()
        {
            return int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        }

        [HttpGet("photographer/{photographerId}")]
        [Authorize]
        public async Task<IActionResult> GetPhotographerSchedule(int photographerId, [FromQuery] DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);

            var schedule = await _context.Schedules
                .Where(s => s.PhotographerId == photographerId &&
                           s.StartTime >= startOfDay &&
                           s.StartTime < endOfDay)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            var bookings = await _context.Orders
                .Where(o => o.PhotographerId == photographerId &&
                           o.OrderDate >= startOfDay &&
                           o.OrderDate < endOfDay &&
                           o.Status != OrderStatus.Cancelled)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.Status,
                    ClientName = o.Client != null ? $"{o.Client.FirstName} {o.Client.LastName}" : "Unknown"
                })
                .ToListAsync();

            return Ok(new { schedule, bookings });
        }

        [HttpPost]
        [Authorize(Roles = "Photographer,Admin")]
        public async Task<IActionResult> CreateSchedule([FromBody] Schedule schedule)
        {
            var userId = GetCurrentUserId();

            if (User.IsInRole("Photographer"))
                schedule.PhotographerId = userId;

            // Check for overlapping schedules
            var overlapping = await _context.Schedules
                .AnyAsync(s => s.PhotographerId == schedule.PhotographerId &&
                              ((schedule.StartTime >= s.StartTime && schedule.StartTime < s.EndTime) ||
                               (schedule.EndTime > s.StartTime && schedule.EndTime <= s.EndTime) ||
                               (schedule.StartTime <= s.StartTime && schedule.EndTime >= s.EndTime)));

            if (overlapping)
                return BadRequest(new { message = "Schedule overlaps with existing schedule" });

            await _context.Schedules.AddAsync(schedule);
            await _context.SaveChangesAsync();

            return Ok(schedule);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Photographer,Admin")]
        public async Task<IActionResult> UpdateSchedule(int id, [FromBody] Schedule schedule)
        {
            var existingSchedule = await _context.Schedules.FindAsync(id);

            if (existingSchedule == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (User.IsInRole("Photographer") && existingSchedule.PhotographerId != userId)
                return Forbid();

            existingSchedule.StartTime = schedule.StartTime;
            existingSchedule.EndTime = schedule.EndTime;
            existingSchedule.IsAvailable = schedule.IsAvailable;

            await _context.SaveChangesAsync();

            return Ok(existingSchedule);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Photographer,Admin")]
        public async Task<IActionResult> DeleteSchedule(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);

            if (schedule == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (User.IsInRole("Photographer") && schedule.PhotographerId != userId)
                return Forbid();

            _context.Schedules.Remove(schedule);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Schedule deleted successfully" });
        }

        [HttpGet("available-slots")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] int photographerId, [FromQuery] DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = date.Date.AddDays(1);

            var busySlots = await _context.Schedules
                .Where(s => s.PhotographerId == photographerId &&
                           s.StartTime >= startOfDay &&
                           s.StartTime < endOfDay &&
                           !s.IsAvailable)
                .Select(s => new { s.StartTime, s.EndTime })
                .ToListAsync();

            var bookings = await _context.Orders
                .Where(o => o.PhotographerId == photographerId &&
                           o.OrderDate >= startOfDay &&
                           o.OrderDate < endOfDay &&
                           o.Status != OrderStatus.Cancelled)
                .Select(o => new { StartTime = o.OrderDate, EndTime = o.OrderDate.AddHours(2) })
                .ToListAsync();

            var allBusyTimes = busySlots.Concat(bookings).ToList();

            // Generate available time slots (e.g., hourly from 9 AM to 6 PM)
            var availableSlots = new List<DateTime>();
            var startTime = startOfDay.AddHours(9);
            var endTime = startOfDay.AddHours(18);

            for (var time = startTime; time < endTime; time = time.AddHours(1))
            {
                var isBusy = allBusyTimes.Any(b => time >= b.StartTime && time < b.EndTime);
                if (!isBusy)
                    availableSlots.Add(time);
            }

            return Ok(availableSlots);
        }
    }
}