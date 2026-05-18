using Microsoft.EntityFrameworkCore;
using PhotoStudioCRM.Backend.Models;

namespace PhotoStudioCRM.Backend.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext context)
        {
            await context.Database.MigrateAsync();

            // Seed roles if none exist
            if (!context.Roles.Any())
            {
                var roles = new List<Role>
                {
                    new Role { Name = "Admin" },
                    new Role { Name = "Photographer" },
                    new Role { Name = "Client" },
                    new Role { Name = "Accountant" }
                };

                await context.Roles.AddRangeAsync(roles);
                await context.SaveChangesAsync();
            }

            // Seed admin user if none exist
            if (!context.Users.Any(u => u.Email == "admin@photostudio.com"))
            {
                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                if (adminRole != null)
                {
                    var admin = new User
                    {
                        Email = "admin@photostudio.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                        FirstName = "Admin",
                        LastName = "User",
                        Phone = "+1234567890",
                        RoleId = adminRole.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await context.Users.AddAsync(admin);
                    await context.SaveChangesAsync();
                }
            }

            // Seed sample services with Russian names
            if (!context.Services.Any())
            {
                var services = new List<Service>
                {
                    new Service { Name = "📸 Портретная фотосессия", Description = "Профессиональная портретная съемка в студии. Включает: 1 час съемки, 30 обработанных фото.", Price = 5000.00m, DurationMinutes = 60, IsActive = true },
                    new Service { Name = "💍 Свадебная фотосессия", Description = "Полный день свадебной съемки. Включает: 8 часов съемки, 300+ фото, фотоальбом.", Price = 35000.00m, DurationMinutes = 480, IsActive = true },
                    new Service { Name = "💕 Love Story", Description = "Романтическая фотосессия для пар. Включает: 2 часа съемки, 50 обработанных фото.", Price = 8000.00m, DurationMinutes = 120, IsActive = true },
                    new Service { Name = "👨‍👩‍👧‍👦 Семейная фотосессия", Description = "Фотосессия для всей семьи. Включает: 1.5 часа съемки, 40 обработанных фото.", Price = 7000.00m, DurationMinutes = 90, IsActive = true },
                    new Service { Name = "🧸 Детская фотосессия", Description = "Яркая и веселая фотосессия для детей. Включает: 1 час съемки, 25 обработанных фото.", Price = 4500.00m, DurationMinutes = 60, IsActive = true },
                    new Service { Name = "🤰 Фотосессия беременных", Description = "Нежная фотосессия для будущих мам. Включает: 1.5 часа съемки, 35 обработанных фото.", Price = 6500.00m, DurationMinutes = 90, IsActive = true },
                    new Service { Name = "📦 Предметная съемка", Description = "Профессиональная съемка товаров для интернет-магазинов. Включает: до 20 предметов, обработка.", Price = 3000.00m, DurationMinutes = 60, IsActive = true },
                    new Service { Name = "🎨 Профессиональная ретушь", Description = "Глубокая ретушь фотографий. Включает: цветокоррекцию, ретушь кожи, удаление дефектов.", Price = 1000.00m, DurationMinutes = 30, IsActive = true },
                    new Service { Name = "🏠 Аренда фотостудии", Description = "Аренда профессиональной фотостудии. Включает: световое оборудование, фоны, интерьерные зоны.", Price = 2000.00m, DurationMinutes = 60, IsActive = true },
                    new Service { Name = "🎥 Видеосъемка мероприятия", Description = "Профессиональная видеосъемка. Включает: монтаж, эффекты, музыкальное сопровождение.", Price = 15000.00m, DurationMinutes = 240, IsActive = true }
                };

                await context.Services.AddRangeAsync(services);
                await context.SaveChangesAsync();
            }

            // Seed test users (client, photographer, accountant) if none exist
            if (!context.Users.Any(u => u.Email == "client@test.com"))
            {
                var clientRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Client");
                if (clientRole != null)
                {
                    var client = new User
                    {
                        Email = "client@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Client123!"),
                        FirstName = "Иван",
                        LastName = "Петров",
                        Phone = "+79991234567",
                        RoleId = clientRole.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Users.AddAsync(client);
                }
            }

            if (!context.Users.Any(u => u.Email == "photographer@test.com"))
            {
                var photographerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Photographer");
                if (photographerRole != null)
                {
                    var photographer = new User
                    {
                        Email = "photographer@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Photo123!"),
                        FirstName = "Анна",
                        LastName = "Смирнова",
                        Phone = "+79991112233",
                        RoleId = photographerRole.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Users.AddAsync(photographer);
                }
            }

            if (!context.Users.Any(u => u.Email == "accountant@test.com"))
            {
                var accountantRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Accountant");
                if (accountantRole != null)
                {
                    var accountant = new User
                    {
                        Email = "accountant@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Account123!"),
                        FirstName = "Елена",
                        LastName = "Иванова",
                        Phone = "+79998887766",
                        RoleId = accountantRole.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await context.Users.AddAsync(accountant);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}