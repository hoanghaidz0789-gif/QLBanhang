using final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace final.Data;

public static class DbInitializer
{
    private const string AdminRole = "Admin";

    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
        var environment = scopedServices.GetRequiredService<IHostEnvironment>();

        if (environment.IsEnvironment("Testing"))
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (!await db.Products.AnyAsync(cancellationToken))
        {
            db.Products.AddRange(
                new Product { Name = "iPhone 16 · 128GB", Category = "Điện thoại", Description = "Điện thoại Apple, bộ nhớ 128GB.", Price = 22_990_000m },
                new Product { Name = "MacBook Air · M3", Category = "Laptop", Description = "Laptop Apple mỏng nhẹ, chip M3.", Price = 27_490_000m },
                new Product { Name = "Samsung Galaxy S25", Category = "Điện thoại", Description = "Điện thoại Samsung dòng Galaxy S.", Price = 19_990_000m },
                new Product { Name = "Sony WH-1000XM5", Category = "Phụ kiện", Description = "Tai nghe chống ồn không dây.", Price = 7_490_000m });
            await db.SaveChangesAsync(cancellationToken);
        }

        var logger = scopedServices.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));
        var username = configuration["Admin:Username"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No admin account was seeded. Set Admin:Username and Admin:Password in .NET User Secrets to enable admin access.");
            return;
        }

        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdminRole));
            EnsureSucceeded(roleResult, "create the admin role");
        }

        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByNameAsync(username);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = username };
            var userResult = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(userResult, "create the configured admin user");
        }

        if (!await userManager.IsInRoleAsync(admin, AdminRole))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, AdminRole);
            EnsureSucceeded(roleResult, "assign the admin role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Could not {operation}: {errors}");
        }
    }
}
