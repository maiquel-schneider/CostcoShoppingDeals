using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using CostcoDeals.Data;
using CostcoDeals.Scraper;
using CostcoDeals.Services;

namespace CostcoApp
{
    public partial class App : Application
    {
        // Host for DI and lifetime management
        public IHost AppHost { get; }
        public IServiceProvider Services => AppHost.Services;

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    // Backend services
                    services.AddDbContext<MainDatabase>(opts =>
                        opts.UseSqlite("Data Source=costcodeals.db"));
                    services.AddScoped<IScraperService, CostcoScraperService>();
                    services.AddScoped<ProductParser>();
                    services.AddScoped<ProductManager>();

                    // UI view models and windows
                    services.AddSingleton<ViewModels.MainViewModel>();
                    services.AddTransient<Views.MainWindow>();
                })
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await AppHost.StartAsync();

            // Ensure DB and tables
            using (var scope = AppHost.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MainDatabase>();
                await db.Database.EnsureCreatedAsync();
            }

            // **Use a scope for the window, too**
            using (var scope = AppHost.Services.CreateScope())
            {
                var window = scope.ServiceProvider.GetRequiredService<Views.MainWindow>();
                window.Show();
            }

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            // Stop and dispose host
            await AppHost.StopAsync();
            AppHost.Dispose();

            base.OnExit(e);
        }
    }
}
