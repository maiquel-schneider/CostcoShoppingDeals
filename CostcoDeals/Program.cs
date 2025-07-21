using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CostcoDeals.Data;
using CostcoDeals.Scraper;
using CostcoDeals.Services;
using CostcoDeals.Shared.Enums;
using System.Diagnostics;

namespace CostcoDeals
{
    internal class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    // EF Core DbContext with SQLite
                    services.AddDbContext<MainDatabase>(opts =>
                        opts.UseSqlite("Data Source=costcodeals.db"));

                    // Scraper, parser, and orchestrator
                    services.AddTransient<IScraperService, CostcoScraperService>();
                    services.AddTransient<ProductParser>();
                    services.AddTransient<ProductManager>();
                })
                .ConfigureLogging(log =>
                {
                    log.ClearProviders();
                    log.AddConsole();
                    log.AddFilter("CostcoDeals", LogLevel.Information);
                })
                .Build();

            // Ensure database & tables exist (creates on first run) and pick warehouse
            WarehouseLocation warehouse;
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MainDatabase>();
                await db.Database.EnsureCreatedAsync();

                // default warehouse to the first enum
                warehouse = Enum.GetValues<WarehouseLocation>().Cast<WarehouseLocation>().First();

                // optionally override from command-line
                if (args.Length > 0 &&
                    Enum.TryParse<WarehouseLocation>(args[0], out var parsed))
                {
                    warehouse = parsed;
                }
            }

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            try
            {
                logger.LogInformation("=== CostcoDeals starting up ===");

                // Console-based progress reporter
                var progress = new Progress<int>(percent =>
                {
                    Console.Write($"\rScraping… {percent}%   ");
                });

                // Run the core workflow with the chosen warehouse
                var manager = host.Services.GetRequiredService<ProductManager>();
                await manager.RunAsync(warehouse, progress);

                Console.WriteLine();  // finish the progress line
                logger.LogInformation("=== CostcoDeals completed successfully ===");
                Console.WriteLine("All done. Press any key to exit.");
                Console.ReadKey();
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during run");
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                return 1;
            }
        }
    }
}
