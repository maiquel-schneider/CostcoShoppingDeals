using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CostcoDeals.Data;
using CostcoDeals.Models;
using CostcoDeals.Scraper;
using CostcoDeals.Shared.Enums;
using CostcoDeals.Services;
using System.Xml.Linq;

namespace CostcoDeals.Services
{
    /// <summary>
    /// Coordinates the full workflow: scrape → parse → upsert products → record price history.
    /// </summary>
    public class ProductManager
    {
        private readonly MainDatabase _db;
        private readonly IScraperService _scraper;
        private readonly ProductParser _parser;
        private readonly ILogger<ProductManager> _log;

        public ProductManager(
            MainDatabase db,
            IScraperService scraper,
            ProductParser parser,
            ILogger<ProductManager> log)
        {
            _db = db;
            _scraper = scraper;
            _parser = parser;
            _log = log;
        }
        public IReadOnlyList<ScrapedProduct> LastScrapedProducts { get; private set; }
        = Array.Empty<ScrapedProduct>();

        /// <summary>
        /// Runs the end-to-end import: choose warehouse, scrape,
        /// parse raw text, upsert Products, add PriceHistory entries.
        /// </summary>
        public async Task<string?> RunAsync(WarehouseLocation warehouse,IProgress<int>? progress = null, IProgress<int>? historyProgress = null)
        {
            _log.LogInformation("▶️ Starting scrape for {Warehouse}", warehouse);

            // 2) Scrape raw DTOs
            var rawItems = (await _scraper.ScrapeAsync(warehouse, progress)).ToList();
            int total = rawItems.Count;
            int done = 0;

            LastScrapedProducts = rawItems;

            // Keep track if we added any new products so we can flush early (to get their IDs)
            bool addedNew = false;

            // 3) Upsert each item
            foreach (var raw in rawItems)
            {
                // 3a) Parse it
                var dto = raw;
                if (dto == null) continue;

                // 3b) Look for existing product by CostcoId + warehouse
                var existing = await _db.Products
                    .Include(p => p.PriceHistories)
                    .SingleOrDefaultAsync(p =>
                        p.CostcoId == dto.CostcoId &&
                        p.WarehouseLocationId == (int)warehouse);

                if (existing == null)
                {
                    // 🍃 New product → inherit category/preference if seen before, else Unknown/None
                    // Look for any same-costcoId product across warehouses
                    var template = await _db.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.CostcoId == dto.CostcoId);
                    
                    existing = new Product
                    {
                        CostcoId = dto.CostcoId,
                        Name = dto.Name,
                        Category = template?.Category ?? ProductCategory.Unknown,
                        Preference = template?.Preference ?? Preference.None,
                        WarehouseLocationId = (int)warehouse,
                    };
                    _db.Products.Add(existing);
                    _log.LogInformation("New product added: {Id} – {Name}", dto.CostcoId, dto.Name);
                    addedNew = true;
                }
                else
                {
                    _log.LogInformation("Existing product found: {Id} – will record new price", dto.CostcoId);
                }
                
                // If we just added a new product, SaveChanges now so existing.Id is populated
                if (addedNew)
                {
                    _log.LogInformation("Getting New Product price History");
                    await _db.SaveChangesAsync();
                    _log.LogInformation("Database Saved");
                    addedNew = false;

                    // 3b‑1) Back‑fill its full history (skipping today's row)
                    _log.LogInformation("Calling ScrapePriceHistoryFor");
                    var historyDtos = await _scraper.ScrapePriceHistoryFor(dto.CostcoId);
                    foreach (var h in historyDtos)
                    {
                        _db.PriceHistories.Add(new PriceHistory
                        {
                            ProductId = existing.Id,
                            ScrapedAt = h.Date,
                            FinalPrice = h.FinalPrice,
                            Discount = h.Savings,
                            FullPrice = h.FinalPrice + h.Savings
                        });
                    }
                    // advance the history bar by one step
                    done++;
                    var pct = total == 0
                            ? 100
                            : (done * 100 / total);
                    historyProgress?.Report(pct);
                }

                // 3c) Record price history if the price is different of if it has already passed 30 days
                decimal? price = PriceParsingHelper.ParsePrice(dto.FinalPrice, _log);
                decimal? fullPrice = decimal.TryParse(dto.FullPrice, out var fp) ? fp : null;
                decimal? discount = decimal.TryParse(dto.Discount, out var d) ? d : null;
                var lastHistory = existing.PriceHistories
                    .OrderByDescending(h => h.ScrapedAt)
                    .FirstOrDefault();

                    bool shouldInsert;
                    if (lastHistory == null)
                        shouldInsert = true;
                    else if (lastHistory.FinalPrice != price)
                        shouldInsert = true;
                    else
                        shouldInsert = (DateTime.UtcNow - lastHistory.ScrapedAt).TotalDays > 30;

                    if (shouldInsert)
                    {
                        _db.PriceHistories.Add(new PriceHistory
                        {
                            Product = existing,
                            FinalPrice = price,
                            FullPrice = fullPrice,
                            Discount = discount,
                            ScrapedAt = DateTime.UtcNow
                        });
                    }
            }

            // 4) Save all changes as one transaction
            await _db.SaveChangesAsync();
            historyProgress?.Report(100);
            _log.LogInformation("=== Workflow complete: database updated ===");

            await _scraper.CloseAsync();

            return _scraper.LastUpdateText;
        }
    }
}
